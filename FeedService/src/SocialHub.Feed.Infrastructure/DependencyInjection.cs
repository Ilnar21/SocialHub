using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using SocialHub.Feed.Application.Abstractions;
using SocialHub.Feed.Application.Ranking;
using SocialHub.Feed.Application.Services;
using SocialHub.Feed.Infrastructure.Cache;
using SocialHub.Feed.Infrastructure.External;
using StackExchange.Redis;
using ApplicationFeedService = SocialHub.Feed.Application.Services.FeedService;

namespace SocialHub.Feed.Infrastructure;

/// <summary>
/// Регистрация инфраструктурных компонентов и связывание их с Application-слоем.
/// Вызывается из Program.cs: builder.Services.AddFeedInfrastructure(builder.Configuration).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddFeedInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // --- Опции ---
        services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName));
        services.Configure<ExternalServiceOptions>(configuration.GetSection(ExternalServiceOptions.SectionName));

        // --- Redis ---
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<CacheOptions>>().Value;
            var config = ConfigurationOptions.Parse(opts.ConnectionString);
            config.AbortOnConnectFail = false; // позволяет стартовать без Redis, TC-24
            return ConnectionMultiplexer.Connect(config);
        });
        services.AddSingleton<IFeedCache, RedisFeedCache>();

        // --- Ранжирование ---
        services.AddSingleton<IRanker, PopularityRanker>();

        // --- Application services ---
        services.AddScoped<IFeedService, ApplicationFeedService>();

        // --- HTTP-клиенты соседних сервисов ---
        services.AddHttpClient<ICommunityServiceClient, CommunityServiceClient>((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<ExternalServiceOptions>>().Value;
                client.BaseAddress = new Uri(EnsureTrailingSlash(opts.CommunityServiceUrl));
                client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
                AddInternalToken(client, opts.InternalToken);
            })
            .AddPolicyHandler(GetRetryPolicy());

        services.AddHttpClient<IPostServiceClient, PostServiceClient>((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<ExternalServiceOptions>>().Value;
                client.BaseAddress = new Uri(EnsureTrailingSlash(opts.PostServiceUrl));
                client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
            })
            .AddPolicyHandler(GetRetryPolicy());

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: 2,
                sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(200 * attempt));

    private static string EnsureTrailingSlash(string url) =>
        url.EndsWith('/') ? url : url + "/";

    private static void AddInternalToken(HttpClient client, string? token)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            client.DefaultRequestHeaders.Add("X-Internal-Token", token);
        }
    }
}
