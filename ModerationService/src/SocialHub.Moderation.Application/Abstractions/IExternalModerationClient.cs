using SocialHub.Moderation.Domain.Entities;

namespace SocialHub.Moderation.Application.Abstractions;

public interface IExternalModerationClient
{
    Task DeletePostAsync(string postId, string reason, CancellationToken cancellationToken);
    Task SetUserBlockedAsync(UserBlock block, CancellationToken cancellationToken);
    Task NotifyPostDeletedAsync(string postId, string reason, CancellationToken cancellationToken);
    Task NotifyUserBlockedAsync(UserBlock block, CancellationToken cancellationToken);
}
