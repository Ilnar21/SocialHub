using MongoDB.Bson.Serialization.Attributes;
using SocialHub.Notification.Domain.Enums;
using NotificationEntity = SocialHub.Notification.Domain.Entities.Notification;

namespace SocialHub.Notification.Infrastructure.Persistence;

public sealed class NotificationDocument
{
    [BsonId]
    public Guid Id { get; set; }

    public Guid RecipientUserId { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? SourceEntityId { get; set; }
    public string? SourceService { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }

    public static NotificationDocument FromDomain(NotificationEntity notification)
    {
        var readAt = notification.IsRead ? notification.ReadAtUtc : null;

        return new NotificationDocument
        {
            Id = notification.Id,
            RecipientUserId = notification.RecipientUserId,
            Type = notification.Type,
            Title = notification.Title,
            Message = notification.Message,
            SourceEntityId = notification.SourceEntityId,
            SourceService = notification.SourceService,
            IsRead = notification.IsRead,
            CreatedAtUtc = notification.CreatedAtUtc,
            ReadAtUtc = readAt
        };
    }

    public NotificationEntity ToDomain()
    {
        var notification = new NotificationEntity(
            RecipientUserId,
            Type,
            Title,
            Message,
            CreatedAtUtc,
            SourceEntityId,
            SourceService);

        notification.RestoreIdentity(Id);

        if (IsRead && ReadAtUtc.HasValue)
        {
            notification.MarkAsRead(ReadAtUtc.Value);
        }

        return notification;
    }
}
