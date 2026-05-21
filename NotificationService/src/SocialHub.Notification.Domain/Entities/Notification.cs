using SocialHub.Notification.Domain.Constants;
using SocialHub.Notification.Domain.Enums;

namespace SocialHub.Notification.Domain.Entities;

public sealed class Notification
{
    private Notification()
    {
    }

    public Notification(
        Guid recipientUserId,
        NotificationType type,
        string title,
        string message,
        DateTime createdAtUtc,
        Guid? sourceEntityId = null,
        string? sourceService = null)
    {
        Id = Guid.NewGuid();
        RecipientUserId = recipientUserId;
        Type = type;
        Title = Normalize(title, nameof(title), NotificationLimits.TitleMaxLength);
        Message = Normalize(message, nameof(message), NotificationLimits.MessageMaxLength);
        SourceEntityId = sourceEntityId;
        SourceService = NormalizeOptional(sourceService, NotificationLimits.SourceServiceMaxLength);
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public NotificationType Type { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public Guid? SourceEntityId { get; private set; }
    public string? SourceService { get; private set; }
    public bool IsRead { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ReadAtUtc { get; private set; }

    public void MarkAsRead(DateTime readAtUtc)
    {
        if (IsRead)
        {
            return;
        }

        IsRead = true;
        ReadAtUtc = readAtUtc;
    }

    private static string Normalize(string value, string parameterName, int maxLength)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Value cannot be empty.", parameterName);
        }

        return normalized.Length > maxLength ? normalized[..maxLength] : normalized;
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
