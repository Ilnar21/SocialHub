using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SocialHub.Moderation.Application.Abstractions;
using SocialHub.Moderation.Infrastructure.External;
using SocialHub.Moderation.Infrastructure.Persistence;

namespace SocialHub.Moderation.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ModerationDatabase")
            ?? throw new InvalidOperationException("Connection string 'ModerationDatabase' is required.");

        services.AddSingleton(_ => new NpgsqlDataSourceBuilder(connectionString).Build());
        services.AddScoped<IModerationRepository, PostgresModerationRepository>();
        services.AddHostedService<DatabaseInitializer>();

        services.AddHttpClient("post", client => ConfigureBaseAddress(client, configuration["ExternalServices:PostBaseUrl"]));
        services.AddHttpClient("auth", client => ConfigureBaseAddress(client, configuration["ExternalServices:AuthBaseUrl"]));
        services.AddHttpClient("notifications", client => ConfigureBaseAddress(client, configuration["ExternalServices:NotificationBaseUrl"]));
        services.AddScoped<IExternalModerationClient, ExternalModerationClient>();

        return services;
    }

    private static void ConfigureBaseAddress(HttpClient client, string? baseUrl)
    {
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            client.BaseAddress = new Uri(baseUrl);
        }
    }
}
