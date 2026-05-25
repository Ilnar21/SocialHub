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
///   5. Фильтруем по выбранному периоду, сортируем по выбранному режиму, режем по пагинации.
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

    public async Task<FeedResponse> GetFeedAsync(
        Guid userId,
        int page,
        int limit,
        FeedQueryOptions options,
        CancellationToken ct = default)
    {
        (page, limit) = NormalizePaging(page, limit);
        options ??= FeedQueryOptions.Default;

        // Шаг 1 — горячий путь через кэш.
        var cached = await TryGetFromCacheAsync(userId, page, limit, options, ct);
        if (cached is not null)
        {
            _logger.LogInformation(
                "Feed cache HIT user={UserId} page={Page} limit={Limit} sort={Sort} period={Period}",
                userId,
                page,
                limit,
                options.Sort,
                options.Period);
            return cached with { FromCache = true };
        }

        // Шаг 2 — подписки пользователя.
        var communityIds = await _communityClient.GetUserCommunityIdsAsync(userId, ct);

        // TC-23: нет подписок → пустая лента, не ошибка.
        if (communityIds.Count == 0)
        {
            var empty = new FeedResponse(Array.Empty<FeedItemResponse>(), page, limit, 0, FromCache: false);
            await TrySetCacheAsync(userId, page, limit, options, empty, ct);
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
        var ranked = RankAndPaginate(posts, communityIds, page, limit, options);

        var response = new FeedResponse(ranked.PageItems, page, limit, ranked.Total, FromCache: false);

        // Шаг 6 — запись в кэш.
        await TrySetCacheAsync(userId, page, limit, options, response, ct);
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

    private async Task<FeedResponse?> TryGetFromCacheAsync(
        Guid userId,
        int page,
        int limit,
        FeedQueryOptions options,
        CancellationToken ct)
    {
        try
        {
            return await _cache.GetAsync(userId, page, limit, options, ct);
        }
        catch (Exception ex)
        {
            // TC-24: кэш недоступен — не падаем, идём напрямую в источники.
            _logger.LogWarning(ex, "Feed cache GET failed, degrading to source services (user={UserId})", userId);
            return null;
        }
    }

    private async Task TrySetCacheAsync(
        Guid userId,
        int page,
        int limit,
        FeedQueryOptions options,
        FeedResponse response,
        CancellationToken ct)
    {
        try
        {
            await _cache.SetAsync(userId, page, limit, options, response, ct);
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
        int limit,
        FeedQueryOptions options)
    {
        var communitySet = communityIds.ToHashSet();
        var now = DateTimeOffset.UtcNow;
        var periodStart = GetPeriodStart(options.Period, now);

        var ranked = posts
            // BR-1 + защита: только посты из подписанных сообществ.
            .Where(p => communitySet.Contains(p.CommunityId))
            .Where(p => periodStart is null || p.CreatedAt >= periodStart.Value)
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
            .ToList();

        ranked = ApplySort(ranked, options.Sort);

        var skip = (page - 1) * limit;
        var pageItems = ranked.Skip(skip).Take(limit).ToList();
        return (pageItems, ranked.Count);
    }

    private static List<FeedItemResponse> ApplySort(
        IReadOnlyCollection<FeedItemResponse> posts,
        FeedSortMode sort)
    {
        return sort switch
        {
            FeedSortMode.Newest => posts
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.Score)
                .ToList(),
            FeedSortMode.Discussed => posts
                .OrderByDescending(p => p.Comments)
                .ThenByDescending(p => p.Likes)
                .ThenByDescending(p => p.CreatedAt)
                .ToList(),
            _ => posts
                .OrderByDescending(p => p.Score)
                .ThenByDescending(p => p.CreatedAt)
                .ToList()
        };
    }

    private static DateTimeOffset? GetPeriodStart(FeedPeriod period, DateTimeOffset now)
    {
        return period switch
        {
            FeedPeriod.Day => now.AddDays(-1),
            FeedPeriod.Week => now.AddDays(-7),
            FeedPeriod.Month => now.AddMonths(-1),
            FeedPeriod.Year => now.AddYears(-1),
            _ => null
        };
    }
}
