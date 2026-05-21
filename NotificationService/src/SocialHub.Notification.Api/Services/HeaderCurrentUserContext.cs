using SocialHub.Notification.Application.Abstractions;
using SocialHub.Notification.Application.Exceptions;

namespace SocialHub.Notification.Api.Services;

public sealed class HeaderCurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HeaderCurrentUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid UserId
    {
        get
        {
            var context = _httpContextAccessor.HttpContext
                ?? throw AppException.Unauthorized("HTTP context is not available.");

            if (!context.Request.Headers.TryGetValue("X-User-Id", out var value)
                || !Guid.TryParse(value.FirstOrDefault(), out var userId))
            {
                throw AppException.Unauthorized("Header X-User-Id with a valid user GUID is required until Auth Service is connected.");
            }

            return userId;
        }
    }
}
