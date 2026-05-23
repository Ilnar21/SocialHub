using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using Microsoft.IdentityModel.Tokens;
using Prometheus;
using SocialHub.Feed.Api.Middleware;
using SocialHub.Feed.Api.Security;
using SocialHub.Feed.Api.Services;
using SocialHub.Feed.Api.Swagger;
using SocialHub.Feed.Application.Abstractions;
using SocialHub.Feed.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Контекст текущего пользователя из заголовков, проставленных API Gateway.
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<DevAuthOptions>(builder.Configuration.GetSection(DevAuthOptions.SectionName));
builder.Services.AddScoped<ICurrentUserContext, HeaderCurrentUserContext>();

// Инфраструктура: Redis-кэш, HTTP-клиенты Community/Post Service, ранжирование, FeedService.
builder.Services.AddFeedInfrastructure(builder.Configuration);

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
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SocialHub Feed Service",
        Version = "v1",
        Description = "Лента пользователя: подписки + ранжирование + Redis-кэш.",
    });
    options.OperationFilter<UserHeadersOperationFilter>();
});

var app = builder.Build();

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpMetrics();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapMetrics();

app.Run();

// Открыто для WebApplicationFactory из тестового проекта.
public partial class Program;
