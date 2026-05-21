using MongoDB.Bson.Serialization.Attributes;
using SocialHub.Notification.Domain.Entities;
using SocialHub.Notification.Domain.Enums;

namespace SocialHub.Notification.Infrastructure.Persistence;

public sealed class NotificationEventDocument
{
    [BsonId]
    public Guid Id { get; set; }

    public Guid RecipientUserId { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string SourceService { get; set; } = string.Empty;
    public Guid? SourceEntityId { get; set; }
    public NotificationEventStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public string? LastError { get; set; }

    public static NotificationEventDocument FromDomain(NotificationEvent notificationEvent)
    {
        return new NotificationEventDocument
        {
            Id = notificationEvent.Id,
            RecipientUserId = notificationEvent.RecipientUserId,
            Type = notificationEvent.Type,
            Title = notificationEvent.Title,
            Message = notificationEvent.Message,
            SourceService = notificationEvent.SourceService,
            SourceEntityId = notificationEvent.SourceEntityId,
            Status = notificationEvent.Status,
            CreatedAtUtc = notificationEvent.CreatedAtUtc,
            ProcessedAtUtc = notificationEvent.ProcessedAtUtc,
            AttemptCount = notificationEvent.AttemptCount,
            LastAttemptAtUtc = notificationEvent.LastAttemptAtUtc,
            LastError = notificationEvent.LastError
        };
    }

    public NotificationEvent ToDomain()
    {
        var notificationEvent = new NotificationEvent(
            RecipientUserId,
            Type,
            Title,
            Message,
            SourceService,
            SourceEntityId,
            CreatedAtUtc);

        notificationEvent.Restore(Id, Status, ProcessedAtUtc, AttemptCount, LastAttemptAtUtc, LastError);
        return notificationEvent;
    }
}
