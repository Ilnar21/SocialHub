using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SocialHub.Community.Api.Swagger;

public sealed class UserHeadersOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= [];

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-User-Id",
            In = ParameterLocation.Header,
            Required = true,
            Description = "Temporary user id header until Auth & User Service provides JWT.",
            Schema = new OpenApiSchema { Type = "string", Format = "uuid" }
        });

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-User-Role",
            In = ParameterLocation.Header,
            Required = false,
            Description = "Temporary platform role header.",
            Schema = new OpenApiSchema { Type = "string" }
        });
    }
}
