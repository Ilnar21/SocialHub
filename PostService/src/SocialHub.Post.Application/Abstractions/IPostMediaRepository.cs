using SocialHub.Post.Domain;

namespace SocialHub.Post.Application.Abstractions;

public interface IPostMediaRepository
{
    Task<IReadOnlyCollection<PostMedia>> ListByPostIdAsync(Guid postId, CancellationToken cancellationToken);

    Task AddRangeAsync(IReadOnlyCollection<PostMedia> media, CancellationToken cancellationToken);
}
