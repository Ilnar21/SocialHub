using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace SocialHub.Post.Api.Security;

public static class CurrentUser
{
    public static Guid GetRequiredUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (Guid.TryParse(value, out var userId))
        {
            return userId;
        }

        throw new UnauthorizedAccessException("A valid JWT bearer token is required.");
    }
}
