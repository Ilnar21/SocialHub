namespace SocialHub.Notification.Infrastructure.Persistence;

public sealed class MongoOptions
{
    public const string SectionName = "Mongo";

    public string ConnectionString { get; set; } = "mongodb://localhost:27019";
    public string DatabaseName { get; set; } = "socialhub_notifications";
    public string NotificationsCollection { get; set; } = "notifications";
    public string EventsCollection { get; set; } = "notification_events";
}
