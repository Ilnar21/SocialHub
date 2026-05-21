using SocialHub.Notification.Domain.Entities;

namespace SocialHub.Notification.Application.Abstractions;

public interface INotificationEventRepository
{
    Task AddAsync(NotificationEvent notificationEvent, CancellationToken cancellationToken);
    Task<NotificationEvent?> TryTakeNextAsync(CancellationToken cancellationToken);
    Task SaveAsync(NotificationEvent notificationEvent, CancellationToken cancellationToken);
}
