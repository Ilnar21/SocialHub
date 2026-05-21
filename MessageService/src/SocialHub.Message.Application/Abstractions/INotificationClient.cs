using SocialHub.Message.Application.Models.External;

namespace SocialHub.Message.Application.Abstractions;

public interface INotificationClient
{
    Task NotifyMessageReceivedAsync(MessageReceivedNotification request, CancellationToken cancellationToken);
}
