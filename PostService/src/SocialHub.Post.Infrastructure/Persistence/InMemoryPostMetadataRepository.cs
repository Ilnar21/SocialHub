using System.Collections.Concurrent;
using SocialHub.Post.Application.Abstractions;
using SocialHub.Post.Domain;

namespace SocialHub.Post.Infrastructure.Persistence;

public sealed class InMemoryPostMetadataRepository : IPostMetadataRepository
{
    private readonly ConcurrentDictionary<Guid, PostMetadata> _posts = new();

    public Task<PostMetadata?> GetByIdAsync(Guid postId, CancellationToken cancellationToken)
    {
        _posts.TryGetValue(postId, out var post);
        return Task.FromResult(post);
    }

    public Task<IReadOnlyCollection<PostMetadata>> ListByCommunityAsync(
        Guid communityId,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<PostMetadata> posts = _posts.Values
            .Where(post => post.CommunityId == communityId)
            .ToArray();

        return Task.FromResult(posts);
    }

    public Task<int> CountPublishedByAuthorInCommunitySinceAsync(
        Guid authorId,
        Guid communityId,
        DateTimeOffset since,
        CancellationToken cancellationToken)
    {
        var count = _posts.Values.Count(post =>
            post.AuthorId == authorId &&
            post.CommunityId == communityId &&
            post.Status == PostStatus.Published &&
            post.CreatedAt >= since);

        return Task.FromResult(count);
    }

    public Task AddAsync(PostMetadata metadata, CancellationToken cancellationToken)
    {
        if (!_posts.TryAdd(metadata.Id, metadata))
        {
            throw new InvalidOperationException($"Post {metadata.Id} already exists.");
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(PostMetadata metadata, CancellationToken cancellationToken)
    {
        _posts[metadata.Id] = metadata;
        return Task.CompletedTask;
    }

    public Task<int> DeleteByCommunityAsync(Guid communityId, CancellationToken cancellationToken)
    {
        var deleted = 0;
        foreach (var post in _posts.Values.Where(post => post.CommunityId == communityId).ToArray())
        {
            if (_posts.TryRemove(post.Id, out _))
            {
                deleted++;
            }
        }

        return Task.FromResult(deleted);
    }
}
