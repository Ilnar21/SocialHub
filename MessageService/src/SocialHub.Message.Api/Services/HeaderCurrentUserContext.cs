using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using SocialHub.Message.Api.Security;
using SocialHub.Message.Application.Abstractions;
using SocialHub.Message.Application.Exceptions;

namespace SocialHub.Message.Api.Services;

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
}
