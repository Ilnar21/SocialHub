using SocialHub.Notification.Api.Services;
using SocialHub.Notification.Application.Abstractions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, HeaderCurrentUserContext>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "notification-service" }));

app.UseAuthorization();

app.MapControllers();

app.Run();
