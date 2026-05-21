using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using SocialHub.Community.Api.Middleware;
using SocialHub.Community.Api.Services;
using SocialHub.Community.Api.Swagger;
using SocialHub.Community.Application.Abstractions;
using SocialHub.Community.Application.Services;
using SocialHub.Community.Infrastructure;
using SocialHub.Community.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, HeaderCurrentUserContext>();
builder.Services.AddScoped<ICommunityService, CommunityService>();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SocialHub Community Service",
        Version = "v1",
        Description = "Community management, memberships, roles and suggested posts."
    });
    options.OperationFilter<UserHeadersOperationFilter>();
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "community-service" }));

app.MapControllers();

if (app.Configuration.GetValue("ApplyMigrationsOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<CommunityDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.Run();
