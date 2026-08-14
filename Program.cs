using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using UniSecretApi.Data;
using UniSecretApi.Services;

var builder = WebApplication.CreateBuilder(args);

//Add Controllers
builder.Services.AddControllers();

//Add API Explorer
builder.Services.AddEndpointsApiExplorer();

//Add Swagger / OpenAPI
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

// Register PostgreSQL DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString(
            "DefaultConnection"
        )
    );
});


// Register Application Services
builder.Services.AddScoped<AuthService>();

// Configure JWT Authentication
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

// Add Authorization
builder.Services.AddAuthorization();

// Building the Application
var app = builder.Build();

// Configure Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI();
}

// HTTPS Redirection
app.UseHttpsRedirection();

// Authentication
app.UseAuthentication();

// Authorization
app.UseAuthorization();

// Map Controllers
app.MapControllers();

// Run Application
app.Run();