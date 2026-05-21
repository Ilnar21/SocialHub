using SocialHub.Notification.Domain.Enums;

namespace SocialHub.Notification.Application.Models.Notifications;

public sealed record NotificationResponse(
    Guid Id,
    Guid RecipientUserId,
    NotificationType Type,
    string Title,
    string Message,
    bool IsRead,
    DateTime CreatedAtUtc,
    DateTime? ReadAtUtc);
