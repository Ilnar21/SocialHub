using SocialHub.Notification.Application.Models.Notifications;

namespace SocialHub.Notification.Application.Abstractions;

public interface INotificationService
{
    Task<NotificationResponse> CreateAsync(CreateNotificationRequest request, CancellationToken cancellationToken);
    Task<NotificationListResponse> GetCurrentUserNotificationsAsync(CancellationToken cancellationToken);
    Task<UnreadCountResponse> GetCurrentUserUnreadCountAsync(CancellationToken cancellationToken);
    Task MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken);
}
