using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SocialHub.Community.Application.Abstractions;
using SocialHub.Community.Infrastructure.External;
using SocialHub.Community.Infrastructure.Observability;
using SocialHub.Community.Infrastructure.Persistence;

namespace SocialHub.Community.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ExternalServiceOptions>(configuration.GetSection(ExternalServiceOptions.SectionName));

        services.AddDbContext<CommunityDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("CommunityDatabase")));

        services.AddScoped<ICommunityRepository, CommunityRepository>();
        services.AddTransient<CorrelationIdDelegatingHandler>();

        services.AddHttpClient<INotificationClient, NotificationClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ExternalServiceOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(options.NotificationBaseUrl))
            {
                client.BaseAddress = new Uri(options.NotificationBaseUrl);
            }

            ConfigureExternalClient(client, options);
        })
            .AddHttpMessageHandler<CorrelationIdDelegatingHandler>();

        services.AddHttpClient<IPostServiceClient, PostServiceClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ExternalServiceOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(options.PostBaseUrl))
            {
                client.BaseAddress = new Uri(options.PostBaseUrl);
            }

            ConfigureExternalClient(client, options);
        })
            .AddHttpMessageHandler<CorrelationIdDelegatingHandler>();

        return services;
    }

    private static void ConfigureExternalClient(HttpClient client, ExternalServiceOptions options)
    {
        client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));

        if (!string.IsNullOrWhiteSpace(options.InternalToken))
        {
            client.DefaultRequestHeaders.Add("X-Internal-Token", options.InternalToken);
        }
    }
}
