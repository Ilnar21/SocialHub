namespace SocialHub.Notification.Api.Middleware;

public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        context.Items[HeaderName] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        });

        _logger.LogInformation(
            "Notification request started: {Method} {Path}.",
            context.Request.Method,
            context.Request.Path);

        await _next(context);

        _logger.LogInformation(
            "Notification request completed: {Method} {Path} -> {StatusCode}.",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode);
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var value)
            && !string.IsNullOrWhiteSpace(value))
        {
            return value.ToString();
        }

        return Guid.NewGuid().ToString("N");
    }
}
