using SocialHub.Post.Domain;

namespace SocialHub.Post.Application.Abstractions;

public interface IPostContentRepository
{
    Task<PostContent?> GetByPostIdAsync(Guid postId, CancellationToken cancellationToken);
    Task SaveAsync(PostContent content, CancellationToken cancellationToken);
    Task UpdateAsync(PostContent content, CancellationToken cancellationToken);
}
