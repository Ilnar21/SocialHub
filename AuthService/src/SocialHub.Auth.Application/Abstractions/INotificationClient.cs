namespace SocialHub.Auth.Application.Abstractions;

public interface INotificationClient
{
    Task NotifyAsync(
        Guid recipientUserId,
        string recipientEmail,
        string type,
        string title,
        string message,
        Guid? sourceEntityId,
        CancellationToken cancellationToken);
}
