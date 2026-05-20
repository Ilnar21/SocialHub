namespace SocialHub.Feed.Domain.Constants;

/// <summary>
/// Доменные ограничения и параметры Feed Service.
/// </summary>
public static class FeedLimits
{
    /// <summary>
    /// BR-3: пользователь может состоять максимум в 30 сообществах одновременно.
    /// Лента строится из ≤ 30 источников.
    /// </summary>
    public const int MaxCommunitiesPerUser = 30;

    /// <summary>
    /// NFR-2: верхняя граница ответа /feed.
    /// </summary>
    public const int MaxResponseTimeMs = 5_000;

    /// <summary>
    /// Размер страницы по умолчанию.
    /// </summary>
    public const int DefaultPageSize = 20;

    /// <summary>
    /// Максимально допустимый размер страницы.
    /// </summary>
    public const int MaxPageSize = 100;

    /// <summary>
    /// Сколько постов запрашиваем с Post Service на одну страницу
    /// (overfetch для последующего ранжирования).
    /// </summary>
    public const int OverfetchMultiplier = 5;

    /// <summary>
    /// Жёсткая верхняя граница overfetch (защита от DoS).
    /// </summary>
    public const int MaxOverfetchSize = 500;
}
