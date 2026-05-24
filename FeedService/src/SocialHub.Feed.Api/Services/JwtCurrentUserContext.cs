using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using SocialHub.Feed.Application.Abstractions;

namespace SocialHub.Feed.Api.Services;

public sealed class JwtCurrentUserContext : ICurrentUserContext
{
    public Guid? UserId { get; }

    public bool IsAuthenticated => UserId.HasValue;

    public JwtCurrentUserContext(IHttpContextAccessor accessor)
    {
        var user = accessor.HttpContext?.User;
        if (user is null)
        {
            return;
        }

        var claimValue = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (Guid.TryParse(claimValue, out var parsed))
        {
            UserId = parsed;
        }
    }
}
