using System.Collections.Concurrent;
using SocialHub.Post.Application.Abstractions;
using SocialHub.Post.Domain;

namespace SocialHub.Post.Infrastructure.Persistence;

public sealed class InMemoryPostContentRepository : IPostContentRepository
{
    private readonly ConcurrentDictionary<Guid, PostContent> _contents = new();

    public Task<PostContent?> GetByPostIdAsync(Guid postId, CancellationToken cancellationToken)
    {
        _contents.TryGetValue(postId, out var content);
        return Task.FromResult(content);
    }

    public Task SaveAsync(PostContent content, CancellationToken cancellationToken)
    {
        if (!_contents.TryAdd(content.PostId, content))
        {
            throw new InvalidOperationException($"Content for post {content.PostId} already exists.");
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(PostContent content, CancellationToken cancellationToken)
    {
        _contents[content.PostId] = content;
        return Task.CompletedTask;
    }
}
