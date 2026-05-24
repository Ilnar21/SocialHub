using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using SocialHub.Community.Application.Abstractions;
using SocialHub.Community.Application.Exceptions;

namespace SocialHub.Community.Api.Services;

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

    public string? PlatformRole
    {
        get
        {
            var context = _httpContextAccessor.HttpContext;
            var role = context?.User.FindFirstValue(ClaimTypes.Role);
            if (!string.IsNullOrWhiteSpace(role))
            {
                return role;
            }

            return null;
        }
    }
}
