using SocialHub.Post.Domain;

namespace SocialHub.Post.Application.Posts;

public sealed record CreatePostRequest(
    Guid AuthorId,
    Guid CommunityId,
    string Title,
    string Text,
    IReadOnlyCollection<PostMediaUploadRequest>? Media = null);

public sealed record UpdatePostRequest(Guid ActorId, string? Title, string Text);

public sealed record DeletePostRequest(Guid ActorId);

public sealed record VotePostRequest(int Value);

public sealed record PostMediaUploadRequest(string FileName, string ContentType, string Base64Content);

public sealed record PostMediaResponse(
    Guid Id,
    string FileName,
    string ContentType,
    long Size,
    string ObjectKey);

public sealed record PostMediaDownloadResponse(
    string FileName,
    string ContentType,
    byte[] Content);

public sealed record PostResponse(
    Guid Id,
    Guid AuthorId,
    Guid CommunityId,
    string Title,
    string Text,
    PostStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    int Upvotes,
    int Downvotes,
    int Score,
    int ViewerVote,
    IReadOnlyCollection<PostMediaResponse> Media);

public sealed record PostVoteResponse(
    Guid PostId,
    int Upvotes,
    int Downvotes,
    int Score,
    int ViewerVote);

public sealed record DeleteCommunityPostsResponse(int DeletedPosts, int DeletedMediaFiles);

public sealed record OperationResult<T>(bool Succeeded, T? Value, string? Error, int StatusCode)
{
    public static OperationResult<T> Ok(T value) => new(true, value, null, 200);

    public static OperationResult<T> Created(T value) => new(true, value, null, 201);

    public static OperationResult<T> Fail(string error, int statusCode) => new(false, default, error, statusCode);
}
