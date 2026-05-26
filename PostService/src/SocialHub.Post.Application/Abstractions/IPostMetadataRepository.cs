using SocialHub.Post.Domain;

namespace SocialHub.Post.Application.Abstractions;

public interface IPostMetadataRepository
{
    Task<PostMetadata?> GetByIdAsync(Guid postId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<PostMetadata>> ListByCommunityAsync(Guid communityId, CancellationToken cancellationToken);
    Task<int> CountPublishedByAuthorInCommunitySinceAsync(
        Guid authorId,
        Guid communityId,
        DateTimeOffset since,
        CancellationToken cancellationToken);

    Task AddAsync(PostMetadata metadata, CancellationToken cancellationToken);
    Task UpdateAsync(PostMetadata metadata, CancellationToken cancellationToken);
    Task<int> DeleteByCommunityAsync(Guid communityId, CancellationToken cancellationToken);
}
