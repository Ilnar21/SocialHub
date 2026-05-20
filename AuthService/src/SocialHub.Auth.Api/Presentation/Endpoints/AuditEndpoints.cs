using SocialHub.Auth.Application.Abstractions;

namespace SocialHub.Auth.Api.Presentation.Endpoints;

public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/audit/login", async (
            ILoginAuditRepository repository,
            CancellationToken cancellationToken) =>
        {
            var entries = await repository.ListAsync(cancellationToken);
            return Results.Ok(entries);
        }).WithTags("Audit");

        return endpoints;
    }
}
