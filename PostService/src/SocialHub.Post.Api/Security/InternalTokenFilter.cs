using Microsoft.Extensions.Options;

namespace SocialHub.Post.Api.Security;

public sealed class InternalTokenFilter : IEndpointFilter
{
    private readonly InternalAuthOptions _options;

    public InternalTokenFilter(IOptions<InternalAuthOptions> options)
    {
        _options = options.Value;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (!_options.RequireInternalToken)
        {
            return await next(context);
        }

        if (string.IsNullOrWhiteSpace(_options.Token))
        {
            return Results.Problem("Internal token is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var token = context.HttpContext.Request.Headers[InternalAuthOptions.HeaderName].FirstOrDefault();
        if (!string.Equals(token, _options.Token, StringComparison.Ordinal))
        {
            return Results.Unauthorized();
        }

        return await next(context);
    }
}
