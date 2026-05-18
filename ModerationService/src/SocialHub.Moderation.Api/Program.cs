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

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "moderation-service" }));
app.MapControllers();

app.Run();
