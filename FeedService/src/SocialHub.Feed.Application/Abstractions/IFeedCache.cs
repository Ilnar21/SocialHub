using SocialHub.Feed.Application.Models.Feed;

namespace SocialHub.Feed.Application.Abstractions;

/// <summary>
/// Кэш ленты. Скрывает конкретное хранилище (Redis) от Application-слоя.
/// </summary>
public interface IFeedCache
{
    Task<FeedResponse?> GetAsync(
        Guid userId,
        int page,
        int limit,
        FeedQueryOptions options,
        CancellationToken ct = default);

    Task SetAsync(
        Guid userId,
        int page,
        int limit,
        FeedQueryOptions options,
        FeedResponse response,
        CancellationToken ct = default);

    /// <summary>
    /// Сбрасывает все страницы ленты конкретного пользователя.
    /// Вызывается при инвалидирующих событиях (новый пост в подписанном сообществе).
    /// </summary>
    Task InvalidateAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Проверка доступности кэша — используется healthcheck-ом.
    /// </summary>
    Task<bool> PingAsync(CancellationToken ct = default);
}
