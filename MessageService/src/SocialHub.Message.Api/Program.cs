using SocialHub.Message.Api.Middleware;
using SocialHub.Message.Api.Services;
using SocialHub.Message.Application.Abstractions;
using SocialHub.Message.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, HeaderCurrentUserContext>();
builder.Services.AddScoped<IMessageService, SocialHub.Message.Application.Services.MessageService>();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "message-service" }));
app.MapControllers();

app.Run();
