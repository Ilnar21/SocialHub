using System.ComponentModel.DataAnnotations;
using SocialHub.Notification.Domain.Enums;

namespace SocialHub.Notification.Application.Models.Notifications;

public sealed record CreateNotificationRequest(
    [Required] Guid RecipientUserId,
    NotificationType Type,
    [Required, MaxLength(160)] string Title,
    [Required, MaxLength(2_000)] string Message,
    Guid? SourceEntityId = null,
    string? SourceService = null);
