using SocialHub.Feed.Application.Exceptions;

namespace SocialHub.Feed.Api.Middleware;

/// <summary>
/// Глобальный обработчик исключений: преобразует AppException в HTTP-ответ
/// с правильным статусом, а остальные ошибки — в 500 без раскрытия деталей наружу.
/// Логирование всех сбоев централизовано здесь (NFR-5).
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
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
            _logger.LogWarning(ex, "Application error: {Message}", ex.Message);
            await WriteJsonAsync(context, ex.StatusCode, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while processing {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteJsonAsync(context, StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    private static Task WriteJsonAsync(HttpContext context, int statusCode, string message)
    {
        if (context.Response.HasStarted)
        {
            return Task.CompletedTask;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsJsonAsync(new { error = message });
    }
}
