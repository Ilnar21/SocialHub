using SocialHub.Notification.Domain.Enums;

namespace SocialHub.Notification.Application.Models.Events;

public sealed record NotificationEventResponse(
    Guid Id,
    Guid RecipientUserId,
    NotificationType Type,
    NotificationEventStatus Status,
    string SourceService,
    Guid? SourceEntityId,
    DateTime CreatedAtUtc);
