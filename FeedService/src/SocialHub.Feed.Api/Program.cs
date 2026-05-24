using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Prometheus;
using SocialHub.Feed.Api.Middleware;
using SocialHub.Feed.Api.Security;
using SocialHub.Feed.Api.Services;
using SocialHub.Feed.Api.Swagger;
using SocialHub.Feed.Application.Abstractions;
using SocialHub.Feed.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Current user context is resolved from authenticated JWT claims.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, JwtCurrentUserContext>();

// Infrastructure: Redis cache, service clients, ranking and feed orchestration.
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
        Description = "User feed: subscriptions, ranking and Redis cache.",
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT access token from AuthService."
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
    options.OperationFilter<UserHeadersOperationFilter>();
});

var app = builder.Build();

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseHttpMetrics();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapMetrics();

app.Run();

// Exposed for WebApplicationFactory tests.
public partial class Program;
