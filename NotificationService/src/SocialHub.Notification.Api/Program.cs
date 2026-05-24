using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Prometheus;
using SocialHub.Notification.Api.Middleware;
using SocialHub.Notification.Api.Security;
using SocialHub.Notification.Api.Services;
using SocialHub.Notification.Application.Abstractions;
using SocialHub.Notification.Application.Services;
using SocialHub.Notification.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.Configure<InternalAuthOptions>(builder.Configuration.GetSection(InternalAuthOptions.SectionName));
builder.Services.AddScoped<InternalTokenFilter>();
builder.Services.AddScoped<ICurrentUserContext, JwtCurrentUserContext>();
builder.Services.AddScoped<INotificationService, NotificationService>();
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

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter JWT access token from AuthService."
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            []
        }
    });
});

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseHttpMetrics();
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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapMetrics();

app.Run();
