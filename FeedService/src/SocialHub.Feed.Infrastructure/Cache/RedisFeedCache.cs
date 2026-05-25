using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocialHub.Feed.Application.Abstractions;
using SocialHub.Feed.Application.Models.Feed;
using StackExchange.Redis;

namespace SocialHub.Feed.Infrastructure.Cache;

/// <summary>
/// Реализация IFeedCache поверх StackExchange.Redis.
/// Хранит готовый JSON FeedResponse под ключом
///   {prefix}:user:{userId}:sort:{sort}:period:{period}:page:{page}:limit:{limit}
/// TTL задаётся CacheOptions.TtlSeconds (по умолчанию 5 минут).
/// </summary>
public sealed class RedisFeedCache : IFeedCache
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _redis;
    private readonly CacheOptions _options;
    private readonly ILogger<RedisFeedCache> _logger;

    public RedisFeedCache(
        IConnectionMultiplexer redis,
        IOptions<CacheOptions> options,
        ILogger<RedisFeedCache> logger)
    {
        _redis = redis;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<FeedResponse?> GetAsync(
        Guid userId,
        int page,
        int limit,
        FeedQueryOptions options,
        CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync(BuildKey(userId, page, limit, options));
        if (value.IsNullOrEmpty)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<FeedResponse>(value!, JsonOpts);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize cached feed for user {UserId}", userId);
            return null;
        }
    }

    public async Task SetAsync(
        Guid userId,
        int page,
        int limit,
        FeedQueryOptions options,
        FeedResponse response,
        CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var payload = JsonSerializer.Serialize(response, JsonOpts);
        await db.StringSetAsync(
            BuildKey(userId, page, limit, options),
            payload,
            TimeSpan.FromSeconds(_options.TtlSeconds));
    }

    public Task InvalidateAsync(Guid userId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var pattern = $"{_options.KeyPrefix}:user:{userId:N}:*";

        foreach (var endpoint in _redis.GetEndPoints())
        {
            var server = _redis.GetServer(endpoint);
            if (!server.IsConnected || server.IsReplica)
            {
                continue;
            }

            foreach (var key in server.Keys(pattern: pattern))
            {
                db.KeyDelete(key, CommandFlags.FireAndForget);
            }
        }

        return Task.CompletedTask;
    }

    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var latency = await db.PingAsync();
            return latency > TimeSpan.Zero;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Redis ping failed");
            return false;
        }
    }

    private string BuildKey(Guid userId, int page, int limit, FeedQueryOptions options) =>
        $"{_options.KeyPrefix}:user:{userId:N}:sort:{options.CacheSortKey}:period:{options.CachePeriodKey}:page:{page}:limit:{limit}";
}
