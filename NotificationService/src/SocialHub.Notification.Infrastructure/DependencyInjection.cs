using Microsoft.Extensions.DependencyInjection;
using SocialHub.Notification.Application.Abstractions;
using SocialHub.Notification.Infrastructure.Persistence;

namespace SocialHub.Notification.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<INotificationRepository, InMemoryNotificationRepository>();
        return services;
    }
}
