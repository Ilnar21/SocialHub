using SocialHub.Community.Application.Abstractions;
using SocialHub.Community.Application.Exceptions;

namespace SocialHub.Community.Api.Services;

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

    public string? PlatformRole
    {
        get
        {
            var context = _httpContextAccessor.HttpContext;
            return context?.Request.Headers.TryGetValue("X-User-Role", out var value) == true
                ? value.FirstOrDefault()
                : null;
        }
    }
}
