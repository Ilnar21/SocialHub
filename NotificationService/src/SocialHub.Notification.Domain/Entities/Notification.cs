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
        DeliveryChannel channel,
        DateTime createdAtUtc)
    {
        Id = Guid.NewGuid();
        RecipientUserId = recipientUserId;
        Type = type;
        Title = Normalize(title, 160);
        Message = Normalize(message, 2_000);
        Channel = channel;
        DeliveryStatus = NotificationDeliveryStatus.Pending;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public NotificationType Type { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public DeliveryChannel Channel { get; private set; }
    public NotificationDeliveryStatus DeliveryStatus { get; private set; }
    public string? EmailAddress { get; private set; }
    public string? EmailSubject { get; private set; }
    public string? DeliveryError { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? DeliveredAtUtc { get; private set; }

    public void AddEmailDelivery(string emailAddress, string subject)
    {
        EmailAddress = Normalize(emailAddress, 320);
        EmailSubject = Normalize(subject, 200);
        Channel = DeliveryChannel.Email;
    }

    public void MarkDelivered(DateTime deliveredAtUtc)
    {
        DeliveryStatus = NotificationDeliveryStatus.Delivered;
        DeliveredAtUtc = deliveredAtUtc;
        DeliveryError = null;
    }

    public void MarkFailed(string error)
    {
        DeliveryStatus = NotificationDeliveryStatus.Failed;
        DeliveryError = Normalize(error, 1_000);
    }

    private static string Normalize(string value, int maxLength)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Value cannot be empty.");
        }

        return normalized.Length > maxLength ? normalized[..maxLength] : normalized;
    }
}
