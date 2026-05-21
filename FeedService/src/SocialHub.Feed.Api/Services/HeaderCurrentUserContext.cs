using SocialHub.Feed.Application.Abstractions;

namespace SocialHub.Feed.Api.Services;

/// <summary>
/// Извлекает идентификатор пользователя из заголовка X-User-Id,
/// который проставляет API Gateway (Nginx) после валидации токена.
/// </summary>
public sealed class HeaderCurrentUserContext : ICurrentUserContext
{
    public Guid? UserId { get; }

    public bool IsAuthenticated => UserId.HasValue;

    public HeaderCurrentUserContext(IHttpContextAccessor accessor)
    {
        var http = accessor.HttpContext;
        if (http is null)
        {
            return;
        }

        if (http.Request.Headers.TryGetValue("X-User-Id", out var raw)
            && Guid.TryParse(raw.ToString(), out var parsed))
        {
            UserId = parsed;
        }
    }
}
