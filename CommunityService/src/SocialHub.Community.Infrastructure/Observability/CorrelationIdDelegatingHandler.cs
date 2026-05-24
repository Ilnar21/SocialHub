using Microsoft.AspNetCore.Http;

namespace SocialHub.Community.Infrastructure.Observability;

public sealed class CorrelationIdDelegatingHandler : DelegatingHandler
{
    public const string HeaderName = "X-Correlation-Id";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationIdDelegatingHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!request.Headers.Contains(HeaderName))
        {
            request.Headers.TryAddWithoutValidation(HeaderName, ResolveCorrelationId());
        }

        return base.SendAsync(request, cancellationToken);
    }

    private string ResolveCorrelationId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return Guid.NewGuid().ToString("N");
        }

        if (httpContext.Items.TryGetValue(HeaderName, out var itemValue)
            && itemValue is string itemCorrelationId
            && !string.IsNullOrWhiteSpace(itemCorrelationId))
        {
            return itemCorrelationId;
        }

        if (httpContext.Request.Headers.TryGetValue(HeaderName, out var headerValue)
            && !string.IsNullOrWhiteSpace(headerValue))
        {
            return headerValue.ToString();
        }

        return Guid.NewGuid().ToString("N");
    }
}
