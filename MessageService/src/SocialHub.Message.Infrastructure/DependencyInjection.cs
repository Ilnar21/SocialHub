using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using SocialHub.Message.Application.Abstractions;
using SocialHub.Message.Infrastructure.External;
using SocialHub.Message.Infrastructure.Observability;
using SocialHub.Message.Infrastructure.Persistence;

namespace SocialHub.Message.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var mongoOptions = configuration.GetSection("Mongo").Get<MongoOptions>() ?? new MongoOptions();
        services.AddSingleton(mongoOptions);
        services.AddSingleton<IMongoDatabase>(_ =>
        {
            var client = new MongoClient(mongoOptions.ConnectionString);
            return client.GetDatabase(mongoOptions.DatabaseName);
        });

        services.AddScoped<IDialogRepository, MongoDialogRepository>();
        services.AddScoped<IMessageStorageHealthCheck, MongoHealthCheck>();
        services.AddHostedService<MongoIndexInitializer>();
        services.AddTransient<CorrelationIdDelegatingHandler>();

        services.AddHttpClient<INotificationClient, NotificationClient>(client =>
        {
            var baseUrl = configuration["NotificationService:BaseUrl"];
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                client.BaseAddress = new Uri(baseUrl);
            }

            var internalToken = configuration["NotificationService:InternalToken"];
            if (!string.IsNullOrWhiteSpace(internalToken))
            {
                client.DefaultRequestHeaders.Add("X-Internal-Token", internalToken);
            }
        })
            .AddHttpMessageHandler<CorrelationIdDelegatingHandler>();

        return services;
    }
}
