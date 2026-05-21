using Microsoft.Extensions.Logging;
using SocialHub.Feed.Application.Abstractions;
using SocialHub.Feed.Application.Models.External;
using SocialHub.Feed.Application.Models.Feed;
using SocialHub.Feed.Domain.Constants;

namespace SocialHub.Feed.Application.Services;

/// <summary>
/// Основная бизнес-логика ленты.
///
/// Алгоритм GET /feed:
///   1. Пробуем достать страницу из кэша; если он недоступен — деградируем (TC-24).
///   2. Берём список подписок пользователя из Community Service (BR-3: ≤ 30).
///   3. Запрашиваем посты по этим сообществам у Post Service (BR-1: только сообщества).
///   4. Дополнительно фильтруем по списку сообществ (защита от багов Post Service).
///   5. Считаем score через IRanker, сортируем по убыванию, режем по пагинации.
///   6. Кладём результат в кэш с TTL = 5 минут (NFR-2 → быстрое повторное чтение).
/// </summary>
public sealed class FeedService : IFeedService
{
    private readonly ICommunityServiceClient _communityClient;
    private readonly IPostServiceClient _postClient;
    private readonly IFeedCache _cache;
    private readonly IRanker _ranker;
    private readonly ILogger<FeedService> _logger;

    public FeedService(
        ICommunityServiceClient communityClient,
        IPostServiceClient postClient,
        IFeedCache cache,
        IRanker ranker,
        ILogger<FeedService> logger)
    {
        _communityClient = communityClient;
        _postClient = postClient;
        _cache = cache;
        _ranker = ranker;
        _logger = logger;
    }

    public async Task<FeedResponse> GetFeedAsync(Guid userId, int page, int limit, CancellationToken ct = default)
    {
        (page, limit) = NormalizePaging(page, limit);

        // Шаг 1 — горячий путь через кэш.
        var cached = await TryGetFromCacheAsync(userId, page, limit, ct);
        if (cached is not null)
        {
            _logger.LogInformation("Feed cache HIT user={UserId} page={Page} limit={Limit}", userId, page, limit);
            return cached with { FromCache = true };
        }

        // Шаг 2 — подписки пользователя.
        var communityIds = await _communityClient.GetUserCommunityIdsAsync(userId, ct);

        // TC-23: нет подписок → пустая лента, не ошибка.
        if (communityIds.Count == 0)
        {
            var empty = new FeedResponse(Array.Empty<FeedItemResponse>(), page, limit, 0, FromCache: false);
            await TrySetCacheAsync(userId, page, limit, empty, ct);
            return empty;
        }

        // BR-3: даже если внешний сервис вернёт больше 30 — обрежем.
        if (communityIds.Count > FeedLimits.MaxCommunitiesPerUser)
        {
            _logger.LogWarning(
                "Community Service returned {Count} subscriptions for user {UserId}, truncating to {Max} (BR-3)",
                communityIds.Count, userId, FeedLimits.MaxCommunitiesPerUser);
            communityIds = communityIds.Take(FeedLimits.MaxCommunitiesPerUser).ToList();
        }

        // Шаг 3 — посты сообществ. Overfetch, чтобы было из чего ранжировать.
        var fetchLimit = Math.Min(limit * FeedLimits.OverfetchMultiplier, FeedLimits.MaxOverfetchSize);
        var posts = await _postClient.GetPostsByCommunitiesAsync(communityIds, fetchLimit, ct);

        // Шаг 4–5 — фильтрация (защитный фильтр) + ранжирование + пагинация.
        var ranked = RankAndPaginate(posts, communityIds, page, limit);

        var response = new FeedResponse(ranked.PageItems, page, limit, ranked.Total, FromCache: false);

        // Шаг 6 — запись в кэш.
        await TrySetCacheAsync(userId, page, limit, response, ct);
        return response;
    }

    public async Task RefreshAsync(Guid userId, CancellationToken ct = default)
    {
        await _cache.InvalidateAsync(userId, ct);
        _logger.LogInformation("Feed cache refreshed by user {UserId}", userId);
    }

    public async Task InvalidateAsync(Guid userId, CancellationToken ct = default)
    {
        await _cache.InvalidateAsync(userId, ct);
        _logger.LogInformation("Feed cache invalidated for user {UserId}", userId);
    }

    private static (int page, int limit) NormalizePaging(int page, int limit)
    {
        if (page < 1) page = 1;
        if (limit < 1) limit = FeedLimits.DefaultPageSize;
        if (limit > FeedLimits.MaxPageSize) limit = FeedLimits.MaxPageSize;
        return (page, limit);
    }

    private async Task<FeedResponse?> TryGetFromCacheAsync(Guid userId, int page, int limit, CancellationToken ct)
    {
        try
        {
            return await _cache.GetAsync(userId, page, limit, ct);
        }
        catch (Exception ex)
        {
            // TC-24: кэш недоступен — не падаем, идём напрямую в источники.
            _logger.LogWarning(ex, "Feed cache GET failed, degrading to source services (user={UserId})", userId);
            return null;
        }
    }

    private async Task TrySetCacheAsync(Guid userId, int page, int limit, FeedResponse response, CancellationToken ct)
    {
        try
        {
            await _cache.SetAsync(userId, page, limit, response, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Feed cache SET failed (user={UserId})", userId);
        }
    }

    private (IReadOnlyList<FeedItemResponse> PageItems, int Total) RankAndPaginate(
        IReadOnlyList<PostSnapshot> posts,
        IReadOnlyList<Guid> communityIds,
        int page,
        int limit)
    {
        var communitySet = communityIds.ToHashSet();
        var now = DateTimeOffset.UtcNow;

        var ranked = posts
            // BR-1 + защита: только посты из подписанных сообществ.
            .Where(p => communitySet.Contains(p.CommunityId))
            .Select(p => new FeedItemResponse(
                PostId: p.Id,
                CommunityId: p.CommunityId,
                AuthorId: p.AuthorId,
                Title: p.Title,
                PreviewText: p.PreviewText,
                Likes: p.Likes,
                Comments: p.Comments,
                CreatedAt: p.CreatedAt,
                Score: _ranker.Score(p, now)))
            .OrderByDescending(p => p.Score)
            .ThenByDescending(p => p.CreatedAt)
            .ToList();

        var skip = (page - 1) * limit;
        var pageItems = ranked.Skip(skip).Take(limit).ToList();
        return (pageItems, ranked.Count);
    }
}
