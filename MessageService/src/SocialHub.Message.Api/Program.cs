using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SocialHub.Message.Api.Middleware;
using SocialHub.Message.Api.Security;
using SocialHub.Message.Api.Services;
using SocialHub.Message.Application.Abstractions;
using SocialHub.Message.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.Configure<DevAuthOptions>(builder.Configuration.GetSection(DevAuthOptions.SectionName));
builder.Services.AddScoped<ICurrentUserContext, HeaderCurrentUserContext>();
builder.Services.AddScoped<IMessageService, SocialHub.Message.Application.Services.MessageService>();
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
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.MapGet("/health", async (IMessageStorageHealthCheck storage, CancellationToken cancellationToken) =>
{
    var mongoHealthy = await storage.IsHealthyAsync(cancellationToken);
    var status = mongoHealthy ? "ok" : "degraded";

    return Results.Json(new
    {
        status,
        service = "message-service",
        dependencies = new { mongo = mongoHealthy ? "ok" : "unavailable" }
    }, statusCode: mongoHealthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
});
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
