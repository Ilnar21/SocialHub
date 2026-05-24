using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using SocialHub.Message.Application.Abstractions;
using SocialHub.Message.Application.Exceptions;

namespace SocialHub.Message.Api.Services;

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
}
