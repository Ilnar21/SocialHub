using SocialHub.Notification.Application.Abstractions;
using SocialHub.Notification.Domain.Entities;

namespace SocialHub.Notification.Infrastructure.Persistence;

public sealed class NotImplementedNotificationEventRepository : INotificationEventRepository
{
    public Task AddAsync(NotificationEvent notificationEvent, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("MongoDB event repository will be added with the event endpoint.");
    }

    public Task<NotificationEvent?> TryTakeNextAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<NotificationEvent?>(null);
    }

    public Task SaveAsync(NotificationEvent notificationEvent, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
