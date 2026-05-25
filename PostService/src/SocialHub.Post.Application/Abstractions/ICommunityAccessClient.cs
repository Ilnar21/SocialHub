namespace SocialHub.Post.Application.Abstractions;

public interface ICommunityAccessClient
{
    Task<bool> IsMemberAsync(Guid userId, Guid communityId, CancellationToken cancellationToken);

    Task<bool> IsOwnerAsync(Guid userId, Guid communityId, CancellationToken cancellationToken);

    Task<bool> CanViewPostsAsync(Guid? userId, Guid communityId, CancellationToken cancellationToken);
}
