using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SocialHub.Post.Api.Security;
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
builder.Services.Configure<InternalAuthOptions>(builder.Configuration.GetSection(InternalAuthOptions.SectionName));

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

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

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
    ClaimsPrincipal principal,
    PostService postService,
    CancellationToken cancellationToken) =>
{
    var authorId = GetUserId(principal);
    if (authorId is null)
    {
        return Results.Unauthorized();
    }

    var result = await postService.CreateAsync(request with { AuthorId = authorId.Value }, cancellationToken);
    return ToHttpResult(result, result.Value?.Id);
}).RequireAuthorization();

app.MapPost("/api/posts/from-suggested", async (
    PublishSuggestedPostRequest request,
    HttpContext httpContext,
    IOptions<InternalAuthOptions> internalAuth,
    PostService postService,
    CancellationToken cancellationToken) =>
{
    if (!HasValidInternalToken(httpContext, internalAuth.Value))
    {
        return Results.Unauthorized();
    }

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
    HttpContext httpContext,
    IOptions<InternalAuthOptions> internalAuth,
    PostService postService,
    CancellationToken cancellationToken) =>
{
    if (!HasValidInternalToken(httpContext, internalAuth.Value))
    {
        return Results.Unauthorized();
    }

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
    ClaimsPrincipal principal,
    PostService postService,
    CancellationToken cancellationToken) =>
{
    var actorId = GetUserId(principal);
    if (actorId is null)
    {
        return Results.Unauthorized();
    }

    var result = await postService.UpdateAsync(postId, request with { ActorId = actorId.Value }, cancellationToken);
    return ToHttpResult(result);
}).RequireAuthorization();

app.MapDelete("/posts/{postId:guid}", async (
    Guid postId,
    [FromBody] DeletePostRequest request,
    ClaimsPrincipal principal,
    PostService postService,
    CancellationToken cancellationToken) =>
{
    var actorId = GetUserId(principal);
    if (actorId is null)
    {
        return Results.Unauthorized();
    }

    var result = await postService.DeleteAsync(postId, request with { ActorId = actorId.Value }, cancellationToken);
    return ToHttpResult(result);
}).RequireAuthorization();

app.MapPost("/api/posts/{postId:guid}/moderation-delete", async (
    Guid postId,
    [FromBody] ModerationDeleteRequest request,
    HttpContext httpContext,
    IOptions<InternalAuthOptions> internalAuth,
    PostService postService,
    CancellationToken cancellationToken) =>
{
    if (!HasValidInternalToken(httpContext, internalAuth.Value) && !IsPlatformModerator(httpContext.User))
    {
        return Results.Unauthorized();
    }

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

static Guid? GetUserId(ClaimsPrincipal principal)
{
    var value = principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

    return Guid.TryParse(value, out var userId) ? userId : null;
}

static bool IsPlatformModerator(ClaimsPrincipal principal)
{
    var role = principal.FindFirstValue(ClaimTypes.Role);
    return role is not null
        && (role.Equals("PlatformModerator", StringComparison.OrdinalIgnoreCase)
            || role.Equals("PLATFORM_MODERATOR", StringComparison.OrdinalIgnoreCase)
            || role.Equals("MODERATOR", StringComparison.OrdinalIgnoreCase));
}

static bool HasValidInternalToken(HttpContext httpContext, InternalAuthOptions options)
{
    if (!options.RequireInternalToken)
    {
        return true;
    }

    if (string.IsNullOrWhiteSpace(options.Token)
        || !httpContext.Request.Headers.TryGetValue(InternalAuthOptions.HeaderName, out var providedTokens))
    {
        return false;
    }

    var expected = Encoding.UTF8.GetBytes(options.Token);
    foreach (var provided in providedTokens)
    {
        if (string.IsNullOrEmpty(provided))
        {
            continue;
        }

        if (CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), expected))
        {
            return true;
        }
    }

    return false;
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
