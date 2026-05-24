using System.Text.Json.Serialization;
using SocialHub.Auth.Application;
using SocialHub.Auth.Infrastructure.Configuration;
using SocialHub.Auth.Infrastructure;
using SocialHub.Auth.Infrastructure.Persistence;
using SocialHub.Auth.Infrastructure.Security;
using SocialHub.Auth.Api.Presentation.Endpoints;
using SocialHub.Auth.Api.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using Microsoft.IdentityModel.Tokens;
using System.Text;

DotEnv.Load();
PostgresEnvironment.ApplyConnectionString();
PostgresEnvironment.ApplyJwtSettings();

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.Configure<InternalAuthOptions>(builder.Configuration.GetSection(InternalAuthOptions.SectionName));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SocialHub Auth & User Service",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT access token."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            []
        }
    });
});

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT settings are not configured.");

if (Encoding.UTF8.GetByteCount(jwtOptions.Secret) < 32)
{
    throw new InvalidOperationException("JWT secret must contain at least 32 bytes.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("PlatformModeratorOnly", policy => policy.RequireRole("PlatformModerator"));
});

var app = builder.Build();

await DatabaseInitializer.InitializeAsync(app.Services);

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "AuthService" }));
app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapAuditEndpoints();

app.Run();
