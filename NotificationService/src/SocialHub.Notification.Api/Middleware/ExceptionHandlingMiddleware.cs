using System.Text.Json;
using SocialHub.Notification.Application.Exceptions;

namespace SocialHub.Notification.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException ex)
        {
            await WriteProblemAsync(context, ex.StatusCode, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled notification service exception.");
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, "Internal server error.");
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var payload = new
        {
            status = statusCode,
            title = message,
            traceId = context.TraceIdentifier
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }
}
