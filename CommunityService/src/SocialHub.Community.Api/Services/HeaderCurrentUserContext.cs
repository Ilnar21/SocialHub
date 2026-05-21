using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using SocialHub.Community.Application.Abstractions;
using SocialHub.Community.Application.Exceptions;
using SocialHub.Community.Api.Security;

namespace SocialHub.Community.Api.Services;

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

            if (_devAuthOptions.EnableHeaderFallback
                && context.Request.Headers.TryGetValue("X-User-Id", out var value)
                && Guid.TryParse(value.FirstOrDefault(), out var headerUserId))
            {
                return headerUserId;
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

            return _devAuthOptions.EnableHeaderFallback
                && context?.Request.Headers.TryGetValue("X-User-Role", out var value) == true
                ? value.FirstOrDefault()
                : null;
        }
    }
}
