using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using SocialHub.Notification.Application.Abstractions;
using SocialHub.Notification.Infrastructure.Persistence;

namespace SocialHub.Notification.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MongoOptions>(configuration.GetSection(MongoOptions.SectionName));
        services.AddSingleton<INotificationRepository, InMemoryNotificationRepository>();
        return services;
    }
}
