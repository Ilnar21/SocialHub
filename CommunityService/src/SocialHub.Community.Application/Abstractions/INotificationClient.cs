using SocialHub.Community.Application.Models.External;

namespace SocialHub.Community.Application.Abstractions;

public interface INotificationClient
{
    Task<bool> NotifyAsync(InternalNotificationRequest request, CancellationToken cancellationToken);
    Task<bool> NotifyEventAsync(InternalNotificationRequest request, CancellationToken cancellationToken);
}
