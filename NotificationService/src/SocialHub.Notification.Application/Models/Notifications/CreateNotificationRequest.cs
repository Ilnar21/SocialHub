using System.ComponentModel.DataAnnotations;
using SocialHub.Notification.Domain.Constants;
using SocialHub.Notification.Domain.Enums;

namespace SocialHub.Notification.Application.Models.Notifications;

public sealed record CreateNotificationRequest(
    [Required] Guid RecipientUserId,
    NotificationType Type,
    [Required, MaxLength(NotificationLimits.TitleMaxLength)] string Title,
    [Required, MaxLength(NotificationLimits.MessageMaxLength)] string Message,
    Guid? SourceEntityId = null,
    [MaxLength(NotificationLimits.SourceServiceMaxLength)] string? SourceService = null);
