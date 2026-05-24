using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using SocialHub.Notification.Application.Abstractions;
using SocialHub.Notification.Application.Exceptions;

namespace SocialHub.Notification.Api.Services;

public sealed class JwtCurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public JwtCurrentUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid UserId
    {
        get
        {
            var context = _httpContextAccessor.HttpContext
                ?? throw AppException.Unauthorized("HTTP context is not available.");

            var claimValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context.User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (Guid.TryParse(claimValue, out var jwtUserId))
            {
                return jwtUserId;
            }

            throw AppException.Unauthorized("A valid JWT bearer token is required.");
        }
    }
}
