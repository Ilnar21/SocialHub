using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

internal static class SocialHubTracingExtensions
{
    public static IServiceCollection AddSocialHubTracing(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var serviceName = configuration["OTEL_SERVICE_NAME"];
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            serviceName = environment.ApplicationName;
        }

        services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["service.namespace"] = "socialhub",
                    ["deployment.environment"] = environment.EnvironmentName
                }))
            .WithTracing(tracing =>
            {
                tracing
                    .SetSampler(new AlwaysOnSampler())
                    .AddProcessor(new RedactSensitiveTraceTagsProcessor())
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.Filter = context => !context.Request.Path.StartsWithSegments("/metrics");
                    })
                    .AddHttpClientInstrumentation();

                var endpointValue = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
                if (Uri.TryCreate(endpointValue, UriKind.Absolute, out var endpoint))
                {
                    tracing.AddOtlpExporter(options => options.Endpoint = endpoint);
                }
            });

        return services;
    }

    private sealed class RedactSensitiveTraceTagsProcessor : BaseProcessor<Activity>
    {
        private static readonly string[] TagsToRemove =
        [
            "url.query",
            "http.request.header.authorization",
            "http.request.header.cookie",
            "http.response.header.set-cookie",
            "jwt",
            "token",
            "password",
            "secret"
        ];

        public override void OnEnd(Activity data)
        {
            foreach (var tag in TagsToRemove)
            {
                data.SetTag(tag, null);
            }

            RedactQueryFromUrl(data, "url.full");
            RedactQueryFromUrl(data, "http.url");
        }

        private static void RedactQueryFromUrl(Activity activity, string tagName)
        {
            var value = activity.GetTagItem(tagName)?.ToString();
            if (string.IsNullOrWhiteSpace(value)
                || !Uri.TryCreate(value, UriKind.Absolute, out var uri))
            {
                return;
            }

            activity.SetTag(tagName, uri.GetLeftPart(UriPartial.Path));
        }
    }
}
