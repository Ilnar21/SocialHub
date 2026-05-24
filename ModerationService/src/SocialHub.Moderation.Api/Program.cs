using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Prometheus;
using SocialHub.Moderation.Api.Middleware;
using SocialHub.Moderation.Api.Security;
using SocialHub.Moderation.Api.Services;
using SocialHub.Moderation.Application.Abstractions;
using SocialHub.Moderation.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, JwtCurrentUserContext>();
builder.Services.AddScoped<IModerationService, SocialHub.Moderation.Application.Services.ModerationService>();
builder.Services.AddInfrastructure(builder.Configuration);

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
builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseHttpMetrics();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", async (IModerationStorageHealthCheck storage, CancellationToken cancellationToken) =>
{
    var postgresHealthy = await storage.IsHealthyAsync(cancellationToken);
    var status = postgresHealthy ? "ok" : "degraded";

    return Results.Json(new
    {
        status,
        service = "moderation-service",
        dependencies = new { postgres = postgresHealthy ? "ok" : "unavailable" }
    }, statusCode: postgresHealthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
});
app.MapControllers();
app.MapMetrics();

app.Run();
