using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using SocialHub.Notification.Application.Abstractions;
using SocialHub.Notification.Infrastructure.Email;
using SocialHub.Notification.Infrastructure.Health;
using SocialHub.Notification.Infrastructure.Persistence;
using SocialHub.Notification.Infrastructure.Processing;

namespace SocialHub.Notification.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MongoOptions>(configuration.GetSection(MongoOptions.SectionName));
        services.Configure<NotificationProcessingOptions>(configuration.GetSection(NotificationProcessingOptions.SectionName));
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.AddSingleton<IMongoClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<MongoOptions>>().Value;
            return new MongoClient(options.ConnectionString);
        });
        services.AddSingleton(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<MongoOptions>>().Value;
            var client = serviceProvider.GetRequiredService<IMongoClient>();
            return client.GetDatabase(options.DatabaseName);
        });
        services.AddScoped<INotificationRepository, MongoNotificationRepository>();
        services.AddScoped<INotificationEventRepository, MongoNotificationEventRepository>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<INotificationHealthCheck, MongoNotificationHealthCheck>();
        services.AddHostedService<NotificationEventProcessor>();
        return services;
    }
}
