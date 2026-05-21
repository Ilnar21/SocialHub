using SocialHub.Post.Domain;

namespace SocialHub.Post.Application.Posts;

public sealed record CreatePostRequest(Guid AuthorId, Guid CommunityId, string Title, string Text);

public sealed record UpdatePostRequest(Guid ActorId, string? Title, string Text);

public sealed record DeletePostRequest(Guid ActorId);

public sealed record PostResponse(
    Guid Id,
    Guid AuthorId,
    Guid CommunityId,
    string Title,
    string Text,
    PostStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record OperationResult<T>(bool Succeeded, T? Value, string? Error, int StatusCode)
{
    public static OperationResult<T> Ok(T value) => new(true, value, null, 200);

    public static OperationResult<T> Created(T value) => new(true, value, null, 201);

    public static OperationResult<T> Fail(string error, int statusCode) => new(false, default, error, statusCode);
}
