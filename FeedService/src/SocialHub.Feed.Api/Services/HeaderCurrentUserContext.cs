using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using SocialHub.Feed.Api.Security;
using SocialHub.Feed.Application.Abstractions;

namespace SocialHub.Feed.Api.Services;

public sealed class HeaderCurrentUserContext : ICurrentUserContext
{
    public Guid? UserId { get; }

    public bool IsAuthenticated => UserId.HasValue;

    public HeaderCurrentUserContext(IHttpContextAccessor accessor, IOptions<DevAuthOptions> devAuthOptions)
    {
        var http = accessor.HttpContext;
        if (http is null)
        {
            return;
        }

        var claimValue = http.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? http.User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (Guid.TryParse(claimValue, out var jwtUserId))
        {
            UserId = jwtUserId;
            return;
        }

        if (devAuthOptions.Value.EnableHeaderFallback
            && http.Request.Headers.TryGetValue("X-User-Id", out var raw)
            && Guid.TryParse(raw.ToString(), out var headerUserId))
        {
            UserId = headerUserId;
        }
    }
}
