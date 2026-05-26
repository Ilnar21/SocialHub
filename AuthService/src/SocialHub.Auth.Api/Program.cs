using System.Text.Json.Serialization;
using SocialHub.Auth.Application;
using SocialHub.Auth.Application.Services;
using SocialHub.Auth.Infrastructure.Configuration;
using SocialHub.Auth.Infrastructure;
using SocialHub.Auth.Infrastructure.Persistence;
using SocialHub.Auth.Infrastructure.Security;
using SocialHub.Auth.Api.Middleware;
using SocialHub.Auth.Api.Presentation.Endpoints;
using SocialHub.Auth.Api.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using Microsoft.IdentityModel.Tokens;
using Prometheus;
using System.Text;

DotEnv.Load();
PostgresEnvironment.ApplyConnectionString();
PostgresEnvironment.ApplyJwtSettings();

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    options.UseUtcTimestamp = true;
});
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
builder.Logging.Configure(options =>
{
    options.ActivityTrackingOptions = ActivityTrackingOptions.TraceId
        | ActivityTrackingOptions.SpanId
        | ActivityTrackingOptions.ParentId;
});
builder.Services.AddSocialHubTracing(builder.Configuration, builder.Environment);

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
await BootstrapPlatformModeratorsAsync(app.Services, app.Configuration);

app.UseSwagger();
app.UseSwaggerUI();

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseHttpMetrics();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "AuthService" }));
app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapAuditEndpoints();
app.MapMetrics();

app.Run();

static async Task BootstrapPlatformModeratorsAsync(IServiceProvider services, IConfiguration configuration)
{
    var configuredModerators = configuration["BOOTSTRAP_MODERATORS"];
    if (string.IsNullOrWhiteSpace(configuredModerators))
    {
        return;
    }

    using var scope = services.CreateScope();
    var bootstrap = scope.ServiceProvider.GetRequiredService<PlatformModeratorBootstrapService>();
    var logger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("PlatformModeratorBootstrap");

    var result = await bootstrap.ApplyAsync(configuredModerators, CancellationToken.None);
    if (result.PromotedCount > 0 || result.AlreadyModeratorCount > 0)
    {
        logger.LogInformation(
            "Platform moderator bootstrap finished. Promoted: {PromotedCount}. Already moderators: {AlreadyModeratorCount}.",
            result.PromotedCount,
            result.AlreadyModeratorCount);
    }

    foreach (var skippedEntry in result.SkippedEntries)
    {
        logger.LogWarning("Platform moderator bootstrap skipped entry: {SkippedEntry}", skippedEntry);
    }
}
