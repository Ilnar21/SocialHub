using SocialHub.Auth.Application.Abstractions;
using SocialHub.Auth.Application.Dtos;
using SocialHub.Auth.Application.Mapping;
using SocialHub.Auth.Application.Services;
using SocialHub.Auth.Domain.Enums;
using SocialHub.Auth.Api.Presentation.Http;
using SocialHub.Auth.Api.Security;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;

namespace SocialHub.Auth.Api.Presentation.Endpoints;

public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var users = endpoints.MapGroup("/api/users").WithTags("Users");

        users.MapGet("/", async (IUserRepository repository, CancellationToken cancellationToken) =>
        {
            var result = await repository.ListAsync(cancellationToken);
            return Results.Ok(result.Select(user => user.ToResponse()));
        });

        users.MapGet("/{id:guid}", async (Guid id, IUserRepository repository, CancellationToken cancellationToken) =>
        {
            var user = await repository.FindByIdAsync(id, cancellationToken);
            return user is null
                ? Results.NotFound(new ErrorResponse("user_not_found", "User was not found."))
                : Results.Ok(user.ToResponse());
        });

        users.MapPut("/{id:guid}/profile", async (
            Guid id,
            UpdateProfileRequest request,
            ClaimsPrincipal principal,
            AuthUserService service,
            CancellationToken cancellationToken) =>
        {
            var currentUser = await service.ResolveAccountAsync(principal, cancellationToken);
            if (currentUser is null)
            {
                return Results.Unauthorized();
            }

            if (currentUser.Id != id && currentUser.Role != UserRole.PlatformModerator)
            {
                return Results.Forbid();
            }

            var result = await service.UpdateProfileAsync(id, request, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization();

        users.MapPost("/{id:guid}/block", async (
            Guid id,
            BlockUserRequest request,
            ClaimsPrincipal principal,
            AuthUserService service,
            CancellationToken cancellationToken) =>
        {
            var moderator = await service.ResolveAccountAsync(principal, cancellationToken);
            if (moderator is null)
            {
                return Results.Unauthorized();
            }

            var result = await service.BlockUserAsync(moderator.Id, id, request, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization("PlatformModeratorOnly");

        users.MapPost("/{id:guid}/status", async (
            Guid id,
            SetUserStatusRequest request,
            HttpContext httpContext,
            IOptions<InternalAuthOptions> internalAuth,
            AuthUserService service,
            CancellationToken cancellationToken) =>
        {
            if (!HasValidInternalToken(httpContext, internalAuth.Value))
            {
                return Results.Unauthorized();
            }

            var result = await service.SetStatusAsync(id, request, cancellationToken);
            return result.ToHttpResult();
        });

        return users;
    }

    private static bool HasValidInternalToken(HttpContext httpContext, InternalAuthOptions options)
    {
        if (!options.RequireInternalToken)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(options.Token)
            || !httpContext.Request.Headers.TryGetValue("X-Internal-Token", out var providedTokens))
        {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(options.Token);
        return providedTokens.Any(provided =>
            CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), expected));
    }
}
