using SocialHub.Post.Application.Abstractions;
using SocialHub.Post.Application.Posts;
using SocialHub.Post.Domain;

namespace SocialHub.Post.Application.Tests;

public sealed class PostServiceTests
{
    [Fact]
    public async Task Create_rejects_author_outside_community()
    {
        var service = CreateService(isMember: false);

        var result = await service.CreateAsync(
            new CreatePostRequest(Guid.NewGuid(), Guid.NewGuid(), "Title", "Text"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task Create_rejects_daily_limit()
    {
        var metadata = new InMemoryMetadataRepository
        {
            PublishedCount = PostService.DailyPostLimit
        };
        var service = CreateService(metadata: metadata);

        var result = await service.CreateAsync(
            new CreatePostRequest(Guid.NewGuid(), Guid.NewGuid(), "Title", "Text"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(429, result.StatusCode);
        Assert.Empty(metadata.Items);
    }

    [Fact]
    public async Task Update_rejects_non_author()
    {
        var authorId = Guid.NewGuid();
        var communityId = Guid.NewGuid();
        var metadata = new InMemoryMetadataRepository();
        var content = new InMemoryContentRepository();
        var created = PostMetadata.Create(authorId, communityId, "Original", DateTimeOffset.UtcNow);
        await metadata.AddAsync(created, CancellationToken.None);
        await content.SaveAsync(new PostContent(created.Id, "Original text", DateTimeOffset.UtcNow), CancellationToken.None);
        var service = CreateService(metadata: metadata, content: content);

        var result = await service.UpdateAsync(
            created.Id,
            new UpdatePostRequest(Guid.NewGuid(), "Changed", "Changed text"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal("Original", created.Title);
    }

    [Fact]
    public async Task Create_saves_metadata_and_content_for_member()
    {
        var metadata = new InMemoryMetadataRepository();
        var content = new InMemoryContentRepository();
        var service = CreateService(metadata: metadata, content: content);

        var result = await service.CreateAsync(
            new CreatePostRequest(Guid.NewGuid(), Guid.NewGuid(), "  Title  ", "  Text  "),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(201, result.StatusCode);
        Assert.Single(metadata.Items);
        Assert.Single(content.Items);
        Assert.Equal("Title", result.Value!.Title);
        Assert.Equal("Text", result.Value.Text);
    }

    private static PostService CreateService(
        bool isMember = true,
        InMemoryMetadataRepository? metadata = null,
        InMemoryContentRepository? content = null) =>
        new(
            metadata ?? new InMemoryMetadataRepository(),
            content ?? new InMemoryContentRepository(),
            new FakeCommunityAccessClient(isMember),
            new FixedClock());

    private sealed class InMemoryMetadataRepository : IPostMetadataRepository
    {
        public List<PostMetadata> Items { get; } = [];
        public int PublishedCount { get; init; }

        public Task<PostMetadata?> GetByIdAsync(Guid postId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.FirstOrDefault(post => post.Id == postId));

        public Task<IReadOnlyCollection<PostMetadata>> ListByCommunityAsync(Guid communityId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<PostMetadata>>(Items.Where(post => post.CommunityId == communityId).ToArray());

        public Task<int> CountPublishedByAuthorInCommunitySinceAsync(
            Guid authorId,
            Guid communityId,
            DateTimeOffset since,
            CancellationToken cancellationToken) =>
            Task.FromResult(PublishedCount);

        public Task AddAsync(PostMetadata metadata, CancellationToken cancellationToken)
        {
            Items.Add(metadata);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(PostMetadata metadata, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InMemoryContentRepository : IPostContentRepository
    {
        public List<PostContent> Items { get; } = [];

        public Task<PostContent?> GetByPostIdAsync(Guid postId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.FirstOrDefault(content => content.PostId == postId));

        public Task SaveAsync(PostContent content, CancellationToken cancellationToken)
        {
            Items.Add(content);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(PostContent content, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeCommunityAccessClient(bool isMember) : ICommunityAccessClient
    {
        public Task<bool> IsMemberAsync(Guid userId, Guid communityId, CancellationToken cancellationToken) =>
            Task.FromResult(isMember);
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 5, 23, 12, 0, 0, TimeSpan.Zero);
    }
}
