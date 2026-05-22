namespace SocialHub.Post.Api.Endpoints;

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
