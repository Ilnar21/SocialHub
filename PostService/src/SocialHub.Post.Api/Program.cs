using System.Text.Json.Serialization;
using SocialHub.Post.Api.Configuration;
using SocialHub.Post.Api.Endpoints;
using SocialHub.Post.Application;
using SocialHub.Post.Infrastructure;

EnvFile.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddPostApplication();
builder.Services.AddPostInfrastructure(builder.Configuration);

var app = builder.Build();

app.MapPostEndpoints();

app.Run();
