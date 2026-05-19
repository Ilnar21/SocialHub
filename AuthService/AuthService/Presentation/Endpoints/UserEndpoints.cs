using AuthService.Application.Abstractions;
using AuthService.Application.Dtos;
using AuthService.Application.Mapping;
using AuthService.Application.Services;
using AuthService.Domain.Enums;
using AuthService.Presentation.Http;
using System.Security.Claims;

namespace AuthService.Presentation.Endpoints;

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

        return users;
    }
}
