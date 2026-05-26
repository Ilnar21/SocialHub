using SocialHub.Moderation.Domain.Entities;
using SocialHub.Moderation.Application.Models.External;

namespace SocialHub.Moderation.Application.Abstractions;

public interface IExternalModerationClient
{
    Task<ExternalUserResponse?> GetUserAsync(string userId, CancellationToken cancellationToken);
    Task<SideEffectResult> DeletePostAsync(string postId, string reason, CancellationToken cancellationToken);
    Task<SideEffectResult> SetUserBlockedAsync(UserBlock block, CancellationToken cancellationToken);
    Task<SideEffectResult> SetUserActiveAsync(string userId, CancellationToken cancellationToken);
    Task<SideEffectResult> SetCommunityBlockedAsync(string communityId, string moderatorUserId, string reason, CancellationToken cancellationToken);
    Task<SideEffectResult> SetCommunityActiveAsync(string communityId, string moderatorUserId, CancellationToken cancellationToken);
    Task<SideEffectResult> NotifyPostDeletedAsync(string postId, string reason, CancellationToken cancellationToken);
    Task<SideEffectResult> NotifyUserBlockedAsync(UserBlock block, CancellationToken cancellationToken);
}
