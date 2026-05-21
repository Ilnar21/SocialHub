using System.ComponentModel.DataAnnotations;
using SocialHub.Notification.Domain.Constants;
using SocialHub.Notification.Domain.Enums;

namespace SocialHub.Notification.Application.Models.Events;

public sealed record CreateNotificationEventRequest(
    [Required] Guid RecipientUserId,
    NotificationType Type,
    [Required, MaxLength(NotificationLimits.TitleMaxLength)] string Title,
    [Required, MaxLength(NotificationLimits.MessageMaxLength)] string Message,
    [Required, MaxLength(NotificationLimits.SourceServiceMaxLength)] string SourceService,
    Guid? SourceEntityId = null);
