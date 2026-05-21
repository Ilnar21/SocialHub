using SocialHub.Moderation.Api.Middleware;
using SocialHub.Moderation.Api.Services;
using SocialHub.Moderation.Application.Abstractions;
using SocialHub.Moderation.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, HeaderCurrentUserContext>();
builder.Services.AddScoped<IModerationService, SocialHub.Moderation.Application.Services.ModerationService>();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

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

app.Run();
