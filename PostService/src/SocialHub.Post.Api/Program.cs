using SocialHub.Post.Application;
using SocialHub.Post.Application.Posts;
using SocialHub.Post.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddPostApplication();
builder.Services.AddPostInfrastructure(builder.Configuration);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    service = "SocialHub.Post.Api",
    status = "Healthy",
    utcNow = DateTimeOffset.UtcNow
}));

app.MapPost("/posts", async (
    CreatePostRequest request,
    PostService postService,
    CancellationToken cancellationToken) =>
{
    var result = await postService.CreateAsync(request, cancellationToken);
    return ToHttpResult(result, result.Value?.Id);
});

app.MapGet("/posts/{postId:guid}", async (
    Guid postId,
    PostService postService,
    CancellationToken cancellationToken) =>
{
    var result = await postService.GetAsync(postId, cancellationToken);
    return ToHttpResult(result);
});

app.MapGet("/communities/{communityId:guid}/posts", async (
    Guid communityId,
    PostService postService,
    CancellationToken cancellationToken) =>
{
    var posts = await postService.ListByCommunityAsync(communityId, cancellationToken);
    return Results.Ok(posts);
});

app.MapPut("/posts/{postId:guid}", async (
    Guid postId,
    UpdatePostRequest request,
    PostService postService,
    CancellationToken cancellationToken) =>
{
    var result = await postService.UpdateAsync(postId, request, cancellationToken);
    return ToHttpResult(result);
});

app.MapDelete("/posts/{postId:guid}", async (
    Guid postId,
    [FromBody] DeletePostRequest request,
    PostService postService,
    CancellationToken cancellationToken) =>
{
    var result = await postService.DeleteAsync(postId, request, cancellationToken);
    return ToHttpResult(result);
});

app.Run();

static IResult ToHttpResult<T>(OperationResult<T> result, Guid? createdId = null)
{
    if (result.Succeeded && result.StatusCode == StatusCodes.Status201Created && createdId is not null)
    {
        return Results.Created($"/posts/{createdId}", result.Value);
    }

    if (result.Succeeded)
    {
        return Results.Ok(result.Value);
    }

    return Results.Problem(
        title: result.Error,
        statusCode: result.StatusCode);
}
