namespace SocialHub.Post.Application.Abstractions;

public interface ICommunityAccessClient
{
    Task<bool> IsMemberAsync(Guid userId, Guid communityId, CancellationToken cancellationToken);
}
