using SocialHub.Feed.Api.Middleware;
using SocialHub.Feed.Api.Services;
using SocialHub.Feed.Application.Abstractions;
using SocialHub.Feed.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Контекст текущего пользователя из заголовков, проставленных API Gateway.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, HeaderCurrentUserContext>();

// Инфраструктура: Redis-кэш, HTTP-клиенты Community/Post Service, ранжирование, FeedService.
builder.Services.AddFeedInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();

// Открыто для WebApplicationFactory из тестового проекта.
public partial class Program;
