using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace SocialHub.Notification.Api.Security;

public sealed class InternalTokenFilter : IAsyncActionFilter
{
    private readonly InternalAuthOptions _options;

    public InternalTokenFilter(IOptions<InternalAuthOptions> options)
    {
        _options = options.Value;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!_options.RequireInternalToken)
        {
            await next();
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.Token))
        {
            context.Result = new ObjectResult(new { message = "Internal token is not configured." })
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable
            };
            return;
        }

        var token = context.HttpContext.Request.Headers[InternalAuthOptions.HeaderName].FirstOrDefault();
        if (!string.Equals(token, _options.Token, StringComparison.Ordinal))
        {
            context.Result = new UnauthorizedObjectResult(new { message = "Valid internal service token is required." });
            return;
        }

        await next();
    }
}
