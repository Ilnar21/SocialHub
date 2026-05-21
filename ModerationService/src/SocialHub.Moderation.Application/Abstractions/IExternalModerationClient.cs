using SocialHub.Moderation.Domain.Entities;
using SocialHub.Moderation.Application.Models.External;

namespace SocialHub.Moderation.Application.Abstractions;

public interface IExternalModerationClient
{
    Task<SideEffectResult> DeletePostAsync(string postId, string reason, CancellationToken cancellationToken);
    Task<SideEffectResult> SetUserBlockedAsync(UserBlock block, CancellationToken cancellationToken);
    Task<SideEffectResult> NotifyPostDeletedAsync(string postId, string reason, CancellationToken cancellationToken);
    Task<SideEffectResult> NotifyUserBlockedAsync(UserBlock block, CancellationToken cancellationToken);
}
