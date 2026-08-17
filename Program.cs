using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using UniSecretApi.Data;
using UniSecretApi.Services;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// Controllers
// ============================================================

builder.Services.AddControllers();

// ============================================================
// API Explorer / Swagger
// ============================================================

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

// ============================================================
// PostgreSQL / EF Core
// ============================================================
//
// DbContext is Scoped by default.
//
// EF Core obtains database connections from the underlying
// connection pool when queries execute and releases them when
// the request/operation is complete.
//
// Do not manually keep database connections open.
//
// EnableRetryOnFailure provides limited retry handling for
// transient PostgreSQL/network failures.
// ============================================================

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString(
            "DefaultConnection"),
        npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorCodesToAdd: null);
        });
});

// ============================================================
// Memory Cache
// ============================================================
//
// Memory cache is extremely fast but exists only inside the
// current API instance.
//
// It must NOT be treated as the source of truth.
//
// Redis is used as the shared distributed cache when configured.
// ============================================================

builder.Services.AddMemoryCache();

// ============================================================
// Redis Distributed Cache
// ============================================================
//
// Redis allows multiple API instances behind NGINX to share
// cached data.
//
// Example:
//
// "ConnectionStrings": {
//     "Redis": "localhost:6379"
// }
//
// If Redis is not configured, the application still starts and
// MemoryCache remains available.
// ============================================================

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

// ============================================================
// Application Services
// ============================================================

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CacheService>();

// ============================================================
// Health Checks
// ============================================================
//
// The EF Core health check verifies that the application can
// communicate with the configured database.
//
// Endpoint:
//
// GET /health
//
// This can later be used by NGINX, Docker, Kubernetes, or another
// load-balancing/orchestration system.
// ============================================================

builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();

// ============================================================
// JWT Authentication
// ============================================================

var jwtSettings =
    builder.Configuration.GetSection("JwtSettings");

var secretKey =
    jwtSettings["Secret"]
    ?? throw new InvalidOperationException(
        "JWT Secret is not configured.");

var key =
    Encoding.UTF8.GetBytes(secretKey);

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
        // Require HTTPS metadata outside development.
        //
        // In production, the API should always be accessed through
        // HTTPS. NGINX can terminate TLS and forward requests to
        // the ASP.NET Core instances.
        options.RequireHttpsMetadata =
            !builder.Environment.IsDevelopment();

        // The API does not need to save the JWT after validation.
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

                // Do not allow expired tokens to remain valid
                // because of the default five-minute clock skew.
                ClockSkew = TimeSpan.Zero
            };
    });

// ============================================================
// Authorization
// ============================================================

builder.Services.AddAuthorization();

// ============================================================
// Rate Limiting
// ============================================================
//
// This uses ASP.NET Core's built-in rate-limiting infrastructure.
//
// Instead of AddFixedWindowLimiter(), which was not resolving in
// this project, GlobalLimiter is configured directly with
// PartitionedRateLimiter.
//
// Two limits are used:
//
// 1. Authentication endpoints
//      10 requests per minute per client IP
//
// 2. General API endpoints
//      120 requests per minute per client IP
//
// Authentication receives a stricter limit because operations
// such as BCrypt password verification are CPU-intensive.
//
// The limits should ultimately be adjusted using load testing.
//
// Health checks and Swagger are excluded from the limiter.
// ============================================================

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter =
        PartitionedRateLimiter.Create<HttpContext, string>(
            httpContext =>
            {
                var path =
                    httpContext.Request.Path;

                // ------------------------------------------------
                // Do not rate-limit health checks.
                // ------------------------------------------------

                if (path.StartsWithSegments("/health"))
                {
                    return RateLimitPartition.GetNoLimiter(
                        "health");
                }

                // ------------------------------------------------
                // Do not rate-limit Swagger.
                //
                // Swagger should generally be disabled or
                // protected in production, but excluding it from
                // the API limiter keeps development convenient.
                // ------------------------------------------------

                if (path.StartsWithSegments("/swagger"))
                {
                    return RateLimitPartition.GetNoLimiter(
                        "swagger");
                }

                // ------------------------------------------------
                // Identify the client.
                //
                // NGINX should be configured to preserve the
                // original client IP when this application is
                // deployed behind a reverse proxy.
                // ------------------------------------------------

                var clientIp =
                    httpContext.Connection.RemoteIpAddress
                        ?.ToString()
                    ?? "unknown";

                // ------------------------------------------------
                // Authentication endpoints
                // ------------------------------------------------

                if (path.StartsWithSegments(
                        "/api/v1/auth",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return
                        RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey:
                                $"auth:{clientIp}",
                            factory: _ =>
                                new FixedWindowRateLimiterOptions
                                {
                                    PermitLimit = 10,

                                    Window =
                                        TimeSpan.FromMinutes(1),

                                    QueueLimit = 0,

                                    AutoReplenishment = true
                                });
                }

                // ------------------------------------------------
                // General API endpoints
                // ------------------------------------------------

                return
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey:
                            $"general:{clientIp}",
                        factory: _ =>
                            new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = 120,

                                Window =
                                    TimeSpan.FromMinutes(1),

                                QueueLimit = 0,

                                AutoReplenishment = true
                            });
            });
});

// ============================================================
// Build Application
// ============================================================

var app = builder.Build();

// ============================================================
// Swagger
// ============================================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ============================================================
// HTTPS
// ============================================================

app.UseHttpsRedirection();

// ============================================================
// Rate Limiting
// ============================================================
//
// This middleware must be placed before the endpoints that need
// to be protected by the rate limiter.
// ============================================================

app.UseRateLimiter();

// ============================================================
// Authentication
// ============================================================

app.UseAuthentication();

// ============================================================
// Authorization
// ============================================================

app.UseAuthorization();

// ============================================================
// Health Check
// ============================================================
//
// GET /health
//
// Returns the health state of the application/database.
//
// This endpoint can be used by:
//
// - NGINX
// - Docker
// - Kubernetes
// - Load balancers
// - Monitoring systems
// ============================================================

app.MapHealthChecks("/health");

// ============================================================
// Controllers
// ============================================================

app.MapControllers();

// ============================================================
// Start Application
// ============================================================

app.Run();