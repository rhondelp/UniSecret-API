using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using UniSecretApi.Data;
using UniSecretApi.Services;

var builder = WebApplication.CreateBuilder(args);

// ------------------------------------------------------------
// Controllers
// ------------------------------------------------------------

builder.Services.AddControllers();

// ------------------------------------------------------------
// API Explorer / Swagger
// ------------------------------------------------------------

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    const string bearerScheme = "Bearer";

    options.AddSecurityDefinition(
        bearerScheme,
        new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description =
                "Enter your JWT token.\n\n" +
                "Example:\n" +
                "Bearer eyJhbGciOiJIUzI1NiIs..."
        });

    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(
                bearerScheme,
                document)] = []
        });
});

// ------------------------------------------------------------
// PostgreSQL / EF Core
// ------------------------------------------------------------
//
// DbContext is registered as Scoped, which is the normal lifetime
// for ASP.NET Core web requests.
//
// EF Core obtains and releases database connections as needed.
// Do not manually keep connections open across requests.
// ------------------------------------------------------------

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions =>
        {
            // Retries transient PostgreSQL failures.
            // Keep the retry count modest so failures do not
            // unnecessarily increase request latency.
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorCodesToAdd: null);
        });
});

// ------------------------------------------------------------
// Memory Cache
// ------------------------------------------------------------
//
// Useful for extremely fast local lookups.
//
// IMPORTANT:
// Memory cache is instance-local. It must NOT be treated as the
// source of truth when the API runs on multiple instances.
// ------------------------------------------------------------

builder.Services.AddMemoryCache();

// ------------------------------------------------------------
// Distributed Redis Cache
// ------------------------------------------------------------
//
// Redis allows all API instances behind NGINX to share cached data.
//
// Configuration example:
//
// "ConnectionStrings": {
//   "Redis": "localhost:6379"
// }
// ------------------------------------------------------------

var redisConnection =
    builder.Configuration.GetConnectionString("Redis");

if (!string.IsNullOrWhiteSpace(redisConnection))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
        options.InstanceName = "UniSecretApi:";
    });
}

// ------------------------------------------------------------
// Application Services
// ------------------------------------------------------------

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CacheService>();

// ------------------------------------------------------------
// Health Checks
// ------------------------------------------------------------

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();

// ------------------------------------------------------------
// JWT Authentication
// ------------------------------------------------------------

var jwtSettings = builder.Configuration.GetSection("JwtSettings");

var secretKey = jwtSettings["Secret"]
    ?? throw new InvalidOperationException(
        "JWT Secret is not configured.");

var key = Encoding.UTF8.GetBytes(secretKey);

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;

        options.DefaultScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        // In production, HTTPS metadata should be required.
        options.RequireHttpsMetadata =
            !builder.Environment.IsDevelopment();

        // The API does not need to store the JWT after validating it.
        // Keeping SaveToken disabled avoids unnecessary work/state.
        options.SaveToken = false;

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,

                IssuerSigningKey =
                    new SymmetricSecurityKey(key),

                ValidateIssuer = true,

                ValidIssuer =
                    jwtSettings["Issuer"],

                ValidateAudience = true,

                ValidAudience =
                    jwtSettings["Audience"],

                ValidateLifetime = true,

                ClockSkew = TimeSpan.Zero
            };
    });

// ------------------------------------------------------------
// Authorization
// ------------------------------------------------------------

builder.Services.AddAuthorization();

// ------------------------------------------------------------
// ASP.NET Core Rate Limiting
// ------------------------------------------------------------
//
// Authentication endpoints are particularly expensive because
// password hashing is CPU-intensive.
//
// A fixed-window policy prevents one client from consuming excessive
// server resources.
//
// The exact limits should ultimately be adjusted using load testing.
// ------------------------------------------------------------

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter(
        "auth",
        limiterOptions =>
        {
            limiterOptions.PermitLimit = 10;
            limiterOptions.Window = TimeSpan.FromMinutes(1);
            limiterOptions.QueueLimit = 0;
        });

    options.AddFixedWindowLimiter(
        "general",
        limiterOptions =>
        {
            limiterOptions.PermitLimit = 120;
            limiterOptions.Window = TimeSpan.FromMinutes(1);
            limiterOptions.QueueLimit = 0;
        });
});

// ------------------------------------------------------------
// Build
// ------------------------------------------------------------

var app = builder.Build();

// ------------------------------------------------------------
// Swagger
// ------------------------------------------------------------

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ------------------------------------------------------------
// HTTPS
// ------------------------------------------------------------

app.UseHttpsRedirection();

// ------------------------------------------------------------
// Rate Limiting
// ------------------------------------------------------------

app.UseRateLimiter();

// ------------------------------------------------------------
// Authentication / Authorization
// ------------------------------------------------------------

app.UseAuthentication();
app.UseAuthorization();

// ------------------------------------------------------------
// Health endpoint
// ------------------------------------------------------------
//
// Used by NGINX/container orchestration/load-balancing systems
// to determine whether this API instance is healthy.
// ------------------------------------------------------------

app.MapHealthChecks("/health");

// ------------------------------------------------------------
// Controllers
// ------------------------------------------------------------

app.MapControllers();

app.Run();