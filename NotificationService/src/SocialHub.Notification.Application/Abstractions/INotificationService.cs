using SocialHub.Notification.Application.Models.Notifications;
using SocialHub.Notification.Application.Models.Events;

namespace SocialHub.Notification.Application.Abstractions;

public interface INotificationService
{
    Task<NotificationResponse> CreateAsync(CreateNotificationRequest request, CancellationToken cancellationToken);
    Task<NotificationEventResponse> CreateEventAsync(CreateNotificationEventRequest request, CancellationToken cancellationToken);
    Task<NotificationListResponse> GetCurrentUserNotificationsAsync(CancellationToken cancellationToken);
    Task<UnreadCountResponse> GetCurrentUserUnreadCountAsync(CancellationToken cancellationToken);
    Task MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken);
}
