namespace SocialHub.Feed.Application.Models.Feed;

/// <summary>
/// Тело запроса для внутреннего endpoint /feed/invalidate.
/// Сюда же подключится Kafka-consumer, когда появится топик feed.cache.invalidate.
/// </summary>
public sealed record InvalidateFeedRequest(Guid UserId);
