using System.Diagnostics;

namespace SocialHub.Feed.Api.Middleware;

/// <summary>
/// Логирует каждый входящий HTTP-запрос: метод, путь, итоговый статус и длительность.
/// Прокидывает X-Correlation-Id из запроса (или генерирует свой) и добавляет его
/// в logging-scope и в заголовки ответа — это даёт сквозную трассировку запроса
/// между сервисами (NFR-5).
/// </summary>
public sealed class RequestLoggingMiddleware
{
    private const string CorrelationHeader = "X-Correlation-Id";
    private const string UserHeader = "X-User-Id";

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        context.Response.Headers[CorrelationHeader] = correlationId;

        var userId = context.Request.Headers.TryGetValue(UserHeader, out var raw) && !string.IsNullOrWhiteSpace(raw)
            ? raw.ToString()
            : "anonymous";

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["correlationId"] = correlationId,
            ["userId"] = userId,
        });

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogInformation(
                "{Method} {Path} -> {Status} in {Elapsed} ms",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationHeader, out var existing)
            && !string.IsNullOrWhiteSpace(existing))
        {
            return existing.ToString();
        }

        return Guid.NewGuid().ToString("N");
    }
}
