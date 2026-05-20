namespace SocialHub.Feed.Application.Abstractions;

/// <summary>
/// Клиент Community Service. Интерфейс изолирует транспорт (REST/gRPC/Kafka)
/// от бизнес-логики Feed Service — реализацию можно подменить без изменения сервиса.
/// </summary>
public interface ICommunityServiceClient
{
    /// <summary>
    /// Список идентификаторов сообществ, на которые подписан пользователь.
    /// BR-3: возвращается не более 30 идентификаторов.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetUserCommunityIdsAsync(Guid userId, CancellationToken ct = default);
}
