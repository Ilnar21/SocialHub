using NotificationEntity = SocialHub.Notification.Domain.Entities.Notification;

namespace SocialHub.Notification.Application.Abstractions;

public interface INotificationRepository
{
    Task AddAsync(NotificationEntity notification, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<NotificationEntity>> GetByRecipientAsync(Guid recipientUserId, CancellationToken cancellationToken);
    Task<NotificationEntity?> GetByIdAsync(Guid notificationId, CancellationToken cancellationToken);
    Task<int> CountUnreadAsync(Guid recipientUserId, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
