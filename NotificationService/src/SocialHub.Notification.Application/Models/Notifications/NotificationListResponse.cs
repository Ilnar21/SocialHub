namespace SocialHub.Notification.Application.Models.Notifications;

public sealed record NotificationListResponse(
    IReadOnlyCollection<NotificationResponse> Items,
    int UnreadCount);
