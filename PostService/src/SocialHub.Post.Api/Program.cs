using System;
using Amazon.S3;
using Amazon.S3.Model;
using SocialHub.Post.Application;
using SocialHub.Post.Application.Posts;
using SocialHub.Post.Infrastructure;
using SocialHub.Post.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Npgsql;
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

app.MapGet("/health", async (
    NpgsqlDataSource dataSource,
    IAmazonS3 s3,
    IOptions<MinioOptions> minioOptions,
    CancellationToken cancellationToken) =>
{
    var postgresHealthy = await CheckPostgresAsync(dataSource, cancellationToken);
    var minioHealthy = await CheckMinioAsync(s3, minioOptions.Value.BucketName, cancellationToken);
    var healthy = postgresHealthy && minioHealthy;

    return Results.Json(new
    {
        service = "SocialHub.Post.Api",
        status = healthy ? "Healthy" : "Degraded",
        dependencies = new
        {
            postgres = postgresHealthy ? "Healthy" : "Unhealthy",
            minio = minioHealthy ? "Healthy" : "Unhealthy"
        },
        utcNow = DateTimeOffset.UtcNow
    }, statusCode: healthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
});

app.MapPost("/posts", async (
    CreatePostRequest request,
    PostService postService,
    CancellationToken cancellationToken) =>
{
    var result = await postService.CreateAsync(request, cancellationToken);
    return ToHttpResult(result, result.Value?.Id);
});

app.MapPost("/api/posts/from-suggested", async (
    PublishSuggestedPostRequest request,
    PostService postService,
    CancellationToken cancellationToken) =>
{
    var result = await postService.CreateAsync(
        new CreatePostRequest(request.AuthorUserId, request.CommunityId, request.Title, request.Text),
        cancellationToken);

    if (!result.Succeeded || result.Value is null)
    {
        return Results.Ok(new PostPublicationResult(false, null, result.Error));
    }

    return Results.Ok(new PostPublicationResult(true, result.Value.Id, null));
});

app.MapPost("/internal/posts/by-communities", async (
    PostsByCommunitiesRequest request,
    PostService postService,
    CancellationToken cancellationToken) =>
{
    var snapshots = new List<PostSnapshot>();

    foreach (var communityId in request.CommunityIds.Distinct().Take(100))
    {
        var posts = await postService.ListByCommunityAsync(communityId, cancellationToken);
        snapshots.AddRange(posts.Select(ToSnapshot));
    }

    return Results.Ok(
        snapshots
            .OrderByDescending(post => post.CreatedAt)
            .Take(Math.Clamp(request.Limit, 1, 100))
            .ToArray());
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

app.MapPost("/api/posts/{postId:guid}/moderation-delete", async (
    Guid postId,
    [FromBody] ModerationDeleteRequest request,
    PostService postService,
    CancellationToken cancellationToken) =>
{
    var result = await postService.DeleteByModeratorAsync(postId, cancellationToken);
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

static PostSnapshot ToSnapshot(PostResponse post)
{
    var previewText = post.Text.Length <= 240
        ? post.Text
        : post.Text[..240];

    return new PostSnapshot(
        post.Id,
        post.CommunityId,
        post.AuthorId,
        post.Title,
        previewText,
        Likes: 0,
        Comments: 0,
        post.CreatedAt);
}

static async Task<bool> CheckPostgresAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
{
    try
    {
        await using var command = dataSource.CreateCommand("select 1");
        await command.ExecuteScalarAsync(cancellationToken);
        return true;
    }
    catch
    {
        return false;
    }
}

static async Task<bool> CheckMinioAsync(IAmazonS3 s3, string bucketName, CancellationToken cancellationToken)
{
    try
    {
        await s3.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = bucketName,
            MaxKeys = 1
        }, cancellationToken);

        return true;
    }
    catch
    {
        return false;
    }
}

public sealed record PublishSuggestedPostRequest(
    Guid CommunityId,
    Guid AuthorUserId,
    Guid SuggestedPostId,
    string Title,
    string Text);

public sealed record PostPublicationResult(bool Succeeded, Guid? PostId, string? Warning);

public sealed record PostsByCommunitiesRequest(IReadOnlyCollection<Guid> CommunityIds, int Limit);

public sealed record PostSnapshot(
    Guid Id,
    Guid CommunityId,
    Guid AuthorId,
    string Title,
    string PreviewText,
    int Likes,
    int Comments,
    DateTimeOffset CreatedAt);

public sealed record ModerationDeleteRequest(string Reason);
