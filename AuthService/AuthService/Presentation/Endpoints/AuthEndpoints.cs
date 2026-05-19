using AuthService.Application.Dtos;
using AuthService.Application.Services;
using AuthService.Presentation.Http;
using System.Security.Claims;

namespace AuthService.Presentation.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/api/auth").WithTags("Auth");

        auth.MapPost("/register", async (
            RegisterRequest request,
            AuthUserService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.RegisterAsync(request, cancellationToken);
            return result.ToCreated($"/api/users/{result.Value?.Id}");
        });

        auth.MapPost("/login", async (
            LoginRequest request,
            AuthUserService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.LoginAsync(request, cancellationToken);
            return result.ToHttpResult();
        });

        auth.MapGet("/me", async (
            ClaimsPrincipal principal,
            AuthUserService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ResolvePrincipalAsync(principal, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization();

        return auth;
    }
}
