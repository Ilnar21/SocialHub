using SocialHub.Notification.Domain.Enums;

namespace SocialHub.Notification.Domain.Entities;

public sealed class NotificationEvent
{
    private NotificationEvent()
    {
    }

    public NotificationEvent(
        Guid recipientUserId,
        NotificationType type,
        string title,
        string message,
        string sourceService,
        Guid? sourceEntityId,
        string? recipientEmail,
        DateTime createdAtUtc)
    {
        Id = Guid.NewGuid();
        RecipientUserId = recipientUserId;
        Type = type;
        Title = title.Trim();
        Message = message.Trim();
        SourceService = sourceService.Trim();
        SourceEntityId = sourceEntityId;
        RecipientEmail = NormalizeOptional(recipientEmail, 254);
        Status = NotificationEventStatus.New;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public NotificationType Type { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string SourceService { get; private set; } = string.Empty;
    public Guid? SourceEntityId { get; private set; }
    public string? RecipientEmail { get; private set; }
    public NotificationEventStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime? LastAttemptAtUtc { get; private set; }
    public string? LastError { get; private set; }

    public void Restore(Guid id, NotificationEventStatus status, DateTime? processedAtUtc, int attemptCount, DateTime? lastAttemptAtUtc, string? lastError)
    {
        Id = id;
        Status = status;
        ProcessedAtUtc = processedAtUtc;
        AttemptCount = attemptCount;
        LastAttemptAtUtc = lastAttemptAtUtc;
        LastError = lastError;
    }

    public void MarkProcessing(DateTime attemptAtUtc)
    {
        Status = NotificationEventStatus.Processing;
        AttemptCount++;
        LastAttemptAtUtc = attemptAtUtc;
    }

    public void MarkCompleted(DateTime processedAtUtc)
    {
        Status = NotificationEventStatus.Completed;
        ProcessedAtUtc = processedAtUtc;
        LastError = null;
    }

    public void MarkFailed(string error)
    {
        Status = NotificationEventStatus.Failed;
        LastError = error;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length > maxLength ? normalized[..maxLength] : normalized;
    }
}
