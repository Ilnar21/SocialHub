using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SocialHub.Feed.Api.Swagger;

/// <summary>
/// Добавляет служебные заголовки X-User-Id и X-Correlation-Id ко всем операциям Swagger UI,
/// чтобы их можно было задавать прямо из браузера при ручном тестировании
/// (в проде эти заголовки проставляет API Gateway).
/// </summary>
public sealed class UserHeadersOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= new List<OpenApiParameter>();

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-User-Id",
            In = ParameterLocation.Header,
            Required = false,
            Description = "Идентификатор текущего пользователя (Guid). Проставляется API Gateway.",
            Schema = new OpenApiSchema
            {
                Type = "string",
                Format = "uuid",
            },
        });

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Correlation-Id",
            In = ParameterLocation.Header,
            Required = false,
            Description = "Сквозной идентификатор запроса для трассировки в логах.",
            Schema = new OpenApiSchema
            {
                Type = "string",
            },
        });
    }
}
