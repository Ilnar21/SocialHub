namespace SocialHub.Feed.Infrastructure.Cache;

/// <summary>
/// Опции кэша ленты. Привязка к секции "Cache" в appsettings.
/// </summary>
public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    /// <summary>Строка подключения StackExchange.Redis (host:port[,...,password=...]).</summary>
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>TTL кэшированной страницы ленты в секундах (по умолчанию 5 минут).</summary>
    public int TtlSeconds { get; set; } = 300;

    /// <summary>Префикс ключей в Redis.</summary>
    public string KeyPrefix { get; set; } = "feed";
}
