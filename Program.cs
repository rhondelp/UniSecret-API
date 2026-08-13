using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using UniSecretApi.Data;
using UniSecretApi.Services;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// 1. Add Controllers
// ============================================================

builder.Services.AddControllers();

// ============================================================
// 2. Add API Explorer
// ============================================================

builder.Services.AddEndpointsApiExplorer();

// ============================================================
// 3. Add Swagger / OpenAPI
// ============================================================

builder.Services.AddSwaggerGen(options =>
{
    const string bearerScheme = "Bearer";

    // JWT Bearer authentication definition
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
        }
    );

    // Apply Bearer authentication globally to Swagger
    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(
                bearerScheme,
                document
            )] = []
        }
    );
});

// ============================================================
// 4. Register PostgreSQL DbContext
// ============================================================

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString(
            "DefaultConnection"
        )
    );
});

// ============================================================
// 5. Register Application Services
// ============================================================

builder.Services.AddScoped<AuthService>();

// ============================================================
// 6. Configure JWT Authentication
// ============================================================

var jwtSettings = builder.Configuration.GetSection("JwtSettings");

var secretKey = jwtSettings["Secret"]
    ?? throw new InvalidOperationException(
        "JWT Secret is not configured."
    );

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
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;

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

// ============================================================
// 7. Add Authorization
// ============================================================

builder.Services.AddAuthorization();

// ============================================================
// 8. Build Application
// ============================================================

var app = builder.Build();

// ============================================================
// 9. Configure Swagger
// ============================================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI();
}

// ============================================================
// 10. HTTPS Redirection
// ============================================================

app.UseHttpsRedirection();

// ============================================================
// 11. Authentication
// ============================================================

app.UseAuthentication();

// ============================================================
// 12. Authorization
// ============================================================

app.UseAuthorization();

// ============================================================
// 13. Map Controllers
// ============================================================

app.MapControllers();

// ============================================================
// 14. Run Application
// ============================================================

app.Run();