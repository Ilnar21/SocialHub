namespace SocialHub.Notification.Infrastructure.Persistence;

public sealed class NotificationRetentionOptions
{
    public const string SectionName = "NotificationRetention";

    public int NotificationDays { get; set; } = 90;
    public int ReadNotificationDays { get; set; } = 30;
    public int CompletedEventDays { get; set; } = 14;
    public int FailedEventDays { get; set; } = 30;
}
