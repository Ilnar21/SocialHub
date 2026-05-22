using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using SocialHub.Moderation.Api.Security;
using SocialHub.Moderation.Application.Abstractions;
using SocialHub.Moderation.Application.Exceptions;

namespace SocialHub.Moderation.Api.Services;

public sealed class HeaderCurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly DevAuthOptions _devAuthOptions;

    public HeaderCurrentUserContext(
        IHttpContextAccessor httpContextAccessor,
        IOptions<DevAuthOptions> devAuthOptions)
    {
        _httpContextAccessor = httpContextAccessor;
        _devAuthOptions = devAuthOptions.Value;
    }

    public string UserId
    {
        get
        {
            var context = _httpContextAccessor.HttpContext
                ?? throw AppException.Unauthorized("HTTP context is not available.");

            var claimValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context.User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (!string.IsNullOrWhiteSpace(claimValue))
            {
                return claimValue.Trim();
            }

            if (_devAuthOptions.EnableHeaderFallback
                && context.Request.Headers.TryGetValue("X-User-Id", out var value)
                && !string.IsNullOrWhiteSpace(value.FirstOrDefault()))
            {
                return value.First()!.Trim();
            }

            throw AppException.Unauthorized("A valid JWT bearer token is required.");
        }
    }

    public string? PlatformRole
    {
        get
        {
            var context = _httpContextAccessor.HttpContext;
            var role = context?.User.FindFirstValue(ClaimTypes.Role);

            if (!string.IsNullOrWhiteSpace(role))
            {
                return role.Equals("PlatformModerator", StringComparison.OrdinalIgnoreCase)
                    ? "PLATFORM_MODERATOR"
                    : role;
            }

            return _devAuthOptions.EnableHeaderFallback
                && context?.Request.Headers.TryGetValue("X-User-Role", out var value) == true
                    ? value.FirstOrDefault()
                    : null;
        }
    }
}
