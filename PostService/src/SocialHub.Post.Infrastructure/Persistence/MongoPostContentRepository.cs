using Microsoft.Extensions.Options;
using MongoDB.Driver;
using SocialHub.Post.Application.Abstractions;
using SocialHub.Post.Domain;

namespace SocialHub.Post.Infrastructure.Persistence;

public sealed class MongoPostContentRepository : IPostContentRepository
{
    private readonly IMongoCollection<PostContentDocument> _contents;

    public MongoPostContentRepository(IMongoClient client, IOptions<MongoOptions> options)
    {
        var value = options.Value;
        _contents = client
            .GetDatabase(value.DatabaseName)
            .GetCollection<PostContentDocument>(value.PostContentsCollectionName);
    }

    public async Task<PostContent?> GetByPostIdAsync(Guid postId, CancellationToken cancellationToken)
    {
        var document = await _contents
            .Find(content => content.PostId == postId)
            .FirstOrDefaultAsync(cancellationToken);

        return document is null
            ? null
            : new PostContent(document.PostId, document.Text, document.UpdatedAt);
    }

    public Task SaveAsync(PostContent content, CancellationToken cancellationToken)
    {
        return _contents.InsertOneAsync(ToDocument(content), cancellationToken: cancellationToken);
    }

    public Task UpdateAsync(PostContent content, CancellationToken cancellationToken)
    {
        return _contents.ReplaceOneAsync(
            document => document.PostId == content.PostId,
            ToDocument(content),
            new ReplaceOptions { IsUpsert = false },
            cancellationToken);
    }

    private static PostContentDocument ToDocument(PostContent content)
    {
        return new PostContentDocument
        {
            PostId = content.PostId,
            Text = content.Text,
            UpdatedAt = content.UpdatedAt
        };
    }
}
