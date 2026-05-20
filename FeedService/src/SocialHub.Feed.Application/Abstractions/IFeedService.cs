using SocialHub.Feed.Application.Models.Feed;

namespace SocialHub.Feed.Application.Abstractions;

/// <summary>
/// Контракт основного бизнес-сервиса ленты.
/// </summary>
public interface IFeedService
{
    Task<FeedResponse> GetFeedAsync(Guid userId, int page, int limit, CancellationToken ct = default);

    Task RefreshAsync(Guid userId, CancellationToken ct = default);

    Task InvalidateAsync(Guid userId, CancellationToken ct = default);
}
