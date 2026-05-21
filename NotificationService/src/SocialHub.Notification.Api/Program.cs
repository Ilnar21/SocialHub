using System.Text.Json.Serialization;
using SocialHub.Notification.Api.Middleware;
using SocialHub.Notification.Api.Services;
using SocialHub.Notification.Application.Abstractions;
using SocialHub.Notification.Application.Services;
using SocialHub.Notification.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, HeaderCurrentUserContext>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", async (INotificationHealthCheck healthCheck, CancellationToken cancellationToken) =>
{
    var mongoAvailable = await healthCheck.IsMongoAvailableAsync(cancellationToken);
    var status = mongoAvailable ? "ok" : "degraded";
    var statusCode = mongoAvailable ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable;

    return Results.Json(new
    {
        status,
        service = "notification-service",
        mongo = mongoAvailable ? "ok" : "unavailable"
    }, statusCode: statusCode);
});

app.UseAuthorization();

app.MapControllers();

app.Run();
