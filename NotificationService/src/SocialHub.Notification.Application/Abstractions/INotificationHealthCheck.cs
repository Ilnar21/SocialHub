namespace SocialHub.Notification.Application.Abstractions;

public interface INotificationHealthCheck
{
    Task<bool> IsMongoAvailableAsync(CancellationToken cancellationToken);
}
