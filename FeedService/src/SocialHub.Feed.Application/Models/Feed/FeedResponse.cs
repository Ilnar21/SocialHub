namespace SocialHub.Feed.Application.Models.Feed;

/// <summary>
/// Ответ /feed. Page/Limit — текущая страница и её размер, Total — общее количество
/// доступных карточек после фильтрации/ранжирования.
/// </summary>
public sealed record FeedResponse(
    IReadOnlyList<FeedItemResponse> Items,
    int Page,
    int Limit,
    int Total,
    bool FromCache);
