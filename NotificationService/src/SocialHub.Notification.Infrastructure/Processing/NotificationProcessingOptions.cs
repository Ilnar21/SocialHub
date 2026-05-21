namespace SocialHub.Notification.Infrastructure.Processing;

public sealed class NotificationProcessingOptions
{
    public const string SectionName = "NotificationProcessing";

    public int PollingIntervalSeconds { get; set; } = 2;
    public int MaxAttempts { get; set; } = 5;
}
