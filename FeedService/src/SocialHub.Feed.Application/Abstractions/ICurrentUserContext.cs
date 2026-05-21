namespace SocialHub.Feed.Application.Abstractions;

/// <summary>
/// Контекст текущего пользователя, извлекаемый из заголовков, проставленных API Gateway.
/// Конкретная реализация живёт в Api-слое (HeaderCurrentUserContext).
/// </summary>
public interface ICurrentUserContext
{
    Guid? UserId { get; }
    bool IsAuthenticated { get; }
}
