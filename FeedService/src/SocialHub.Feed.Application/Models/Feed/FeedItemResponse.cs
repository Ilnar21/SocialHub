namespace SocialHub.Feed.Application.Models.Feed;

/// <summary>
/// Карточка поста в ленте.
/// </summary>
public sealed record FeedItemResponse(
    Guid PostId,
    Guid CommunityId,
    Guid AuthorId,
    string Title,
    string PreviewText,
    int Likes,
    int Comments,
    DateTimeOffset CreatedAt,
    double Score);
