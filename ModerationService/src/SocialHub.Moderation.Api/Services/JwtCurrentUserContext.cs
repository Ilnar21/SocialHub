using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using SocialHub.Moderation.Application.Abstractions;
using SocialHub.Moderation.Application.Exceptions;

namespace SocialHub.Moderation.Api.Services;

public sealed class JwtCurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public JwtCurrentUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string UserId
    {
        get
        {
            var context = _httpContextAccessor.HttpContext
                ?? throw AppException.Unauthorized("HTTP context is not available.");

            var claimValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context.User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (string.IsNullOrWhiteSpace(claimValue))
            {
                throw AppException.Unauthorized("A valid JWT bearer token is required.");
            }

            return claimValue.Trim();
        }
    }

    public string? PlatformRole
    {
        get
        {
            var role = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role);
            return string.IsNullOrWhiteSpace(role) ? null : role.Trim();
        }
    }
}
