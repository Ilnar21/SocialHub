using SocialHub.Moderation.Application.Abstractions;
using SocialHub.Moderation.Application.Exceptions;

namespace SocialHub.Moderation.Api.Services;

public sealed class HeaderCurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HeaderCurrentUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string UserId
    {
        get
        {
            var context = _httpContextAccessor.HttpContext
                ?? throw AppException.Unauthorized("HTTP context is not available.");

            if (!context.Request.Headers.TryGetValue("X-User-Id", out var value)
                || string.IsNullOrWhiteSpace(value.FirstOrDefault()))
            {
                throw AppException.Unauthorized("Header X-User-Id is required until Auth Service is connected.");
            }

            return value.First()!.Trim();
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
