using System.Text;
using SocialHub.Post.Application.Abstractions;
using SocialHub.Post.Application.Posts;
using SocialHub.Post.Domain;

namespace SocialHub.Post.Application.Tests;

public sealed class PostServiceTests
{
    [Fact]
    public async Task Create_rejects_author_outside_community()
    {
        var service = CreateService(isMember: false, isOwner: false);

        var result = await service.CreateAsync(
            new CreatePostRequest(Guid.NewGuid(), Guid.NewGuid(), "Title", "Text"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task Create_rejects_member_who_is_not_community_owner()
    {
        var service = CreateService(isMember: true, isOwner: false);

        var result = await service.CreateAsync(
            new CreatePostRequest(Guid.NewGuid(), Guid.NewGuid(), "Title", "Text"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal("Only community owner can publish posts directly.", result.Error);
    }

    [Fact]
    public async Task CreateApprovedSuggested_allows_member_after_owner_review()
    {
        var service = CreateService(isMember: true, isOwner: false);

        var result = await service.CreateApprovedSuggestedAsync(
            new CreatePostRequest(Guid.NewGuid(), Guid.NewGuid(), "Title", "Text"),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(201, result.StatusCode);
    }

    [Fact]
    public async Task Create_rejects_daily_limit_before_saving()
    {
        var metadata = new InMemoryMetadataRepository
        {
            PublishedCount = PostService.DailyPostLimit
        };
        var content = new InMemoryContentRepository();
        var storage = new FakeMediaStorage();
        var service = CreateService(metadata: metadata, content: content, storage: storage);

        var result = await service.CreateAsync(
            new CreatePostRequest(Guid.NewGuid(), Guid.NewGuid(), "Title", "Text", [Media("photo.jpg", "image/jpeg", "jpg")]),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(429, result.StatusCode);
        Assert.Empty(metadata.Items);
        Assert.Empty(content.Items);
        Assert.Empty(storage.Uploads);
    }

    [Fact]
    public async Task Create_saves_metadata_text_and_media_for_member()
    {
        var metadata = new InMemoryMetadataRepository();
        var content = new InMemoryContentRepository();
        var media = new InMemoryMediaRepository();
        var storage = new FakeMediaStorage();
        var service = CreateService(metadata: metadata, content: content, media: media, storage: storage);

        var result = await service.CreateAsync(
            new CreatePostRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "  Title  ",
                "  Text  ",
                [Media("photo.jpg", "image/jpeg", "binary")]),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(201, result.StatusCode);
        Assert.Single(metadata.Items);
        Assert.Single(content.Items);
        Assert.Single(media.Items);
        Assert.Single(storage.Uploads);
        Assert.Equal("Title", result.Value!.Title);
        Assert.Equal("Text", result.Value.Text);
        Assert.Equal("photo.jpg", result.Value.Media.Single().FileName);
        Assert.Equal("image/jpeg", result.Value.Media.Single().ContentType);
    }

    [Fact]
    public async Task Create_rejects_invalid_media_payload()
    {
        var service = CreateService();

        var result = await service.CreateAsync(
            new CreatePostRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Title",
                "Text",
                [new PostMediaUploadRequest("photo.jpg", "image/jpeg", "not-base64")]),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Media content must be valid base64.", result.Error);
    }

    [Fact]
    public async Task Create_rejects_more_than_allowed_media_files()
    {
        var media = Enumerable.Range(0, PostService.MaxMediaFiles + 1)
            .Select(index => Media($"photo-{index}.jpg", "image/jpeg", "jpg"))
            .ToArray();
        var service = CreateService();

        var result = await service.CreateAsync(
            new CreatePostRequest(Guid.NewGuid(), Guid.NewGuid(), "Title", "Text", media),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task Update_rejects_non_author_and_preserves_content()
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
        Assert.Equal("Original text", content.Items.Single().Text);
    }

    [Fact]
    public async Task Update_changes_title_and_text_for_author()
    {
        var authorId = Guid.NewGuid();
        var created = await SeedPostAsync(authorId);
        var service = CreateService(metadata: created.Metadata, content: created.Content);

        var result = await service.UpdateAsync(
            created.PostId,
            new UpdatePostRequest(authorId, "New title", "New text"),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("New title", result.Value!.Title);
        Assert.Equal("New text", result.Value.Text);
    }

    [Fact]
    public async Task ListByCommunity_returns_only_published_posts_with_media()
    {
        var authorId = Guid.NewGuid();
        var communityId = Guid.NewGuid();
        var metadata = new InMemoryMetadataRepository();
        var content = new InMemoryContentRepository();
        var media = new InMemoryMediaRepository();
        var published = PostMetadata.Create(authorId, communityId, "Published", DateTimeOffset.UtcNow);
        var deleted = PostMetadata.Create(authorId, communityId, "Deleted", DateTimeOffset.UtcNow.AddMinutes(-1));
        deleted.MarkDeleted(authorId, DateTimeOffset.UtcNow);
        await metadata.AddAsync(published, CancellationToken.None);
        await metadata.AddAsync(deleted, CancellationToken.None);
        await content.SaveAsync(new PostContent(published.Id, "Text", DateTimeOffset.UtcNow), CancellationToken.None);
        await content.SaveAsync(new PostContent(deleted.Id, "Text", DateTimeOffset.UtcNow), CancellationToken.None);
        await media.AddRangeAsync([NewMedia(published.Id, "posts/published/photo.jpg")], CancellationToken.None);
        var service = CreateService(metadata: metadata, content: content, media: media);

        var result = await service.ListByCommunityAsync(communityId, viewerId: null, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(published.Id, result.Single().Id);
        Assert.Single(result.Single().Media);
    }

    [Fact]
    public async Task ListByCommunity_returns_empty_when_viewer_cannot_view_posts()
    {
        var authorId = Guid.NewGuid();
        var communityId = Guid.NewGuid();
        var metadata = new InMemoryMetadataRepository();
        var content = new InMemoryContentRepository();
        var published = PostMetadata.Create(authorId, communityId, "Published", DateTimeOffset.UtcNow);
        await metadata.AddAsync(published, CancellationToken.None);
        await content.SaveAsync(new PostContent(published.Id, "Text", DateTimeOffset.UtcNow), CancellationToken.None);
        var service = CreateService(metadata: metadata, content: content, canViewPosts: false);

        var result = await service.ListByCommunityAsync(communityId, viewerId: Guid.NewGuid(), CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAsync_rejects_when_viewer_cannot_view_posts()
    {
        var created = await SeedPostAsync(Guid.NewGuid());
        var service = CreateService(metadata: created.Metadata, content: created.Content, canViewPosts: false);

        var result = await service.GetAsync(created.PostId, viewerId: Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task GetMedia_returns_file_for_published_post()
    {
        var authorId = Guid.NewGuid();
        var communityId = Guid.NewGuid();
        var metadata = new InMemoryMetadataRepository();
        var content = new InMemoryContentRepository();
        var media = new InMemoryMediaRepository();
        var storage = new FakeMediaStorage();
        var published = PostMetadata.Create(authorId, communityId, "Published", DateTimeOffset.UtcNow);
        var photo = NewMedia(published.Id, "posts/published/photo.jpg");
        await metadata.AddAsync(published, CancellationToken.None);
        await content.SaveAsync(new PostContent(published.Id, "Text", DateTimeOffset.UtcNow), CancellationToken.None);
        await media.AddRangeAsync([photo], CancellationToken.None);
        storage.Files[photo.ObjectKey] = Encoding.UTF8.GetBytes("image-bytes");
        var service = CreateService(metadata: metadata, content: content, media: media, storage: storage);

        var result = await service.GetMediaAsync(published.Id, photo.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("photo.jpg", result.Value!.FileName);
        Assert.Equal("image/jpeg", result.Value.ContentType);
        Assert.Equal("image-bytes", Encoding.UTF8.GetString(result.Value.Content));
    }

    [Fact]
    public async Task GetMedia_rejects_media_from_another_post()
    {
        var first = await SeedPostAsync(Guid.NewGuid());
        var second = await SeedPostAsync(Guid.NewGuid());
        var media = new InMemoryMediaRepository();
        var storage = new FakeMediaStorage();
        var photo = NewMedia(second.PostId, "posts/second/photo.jpg");
        await media.AddRangeAsync([photo], CancellationToken.None);
        var service = CreateService(metadata: first.Metadata, content: first.Content, media: media, storage: storage);

        var result = await service.GetMediaAsync(first.PostId, photo.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Delete_marks_post_deleted_for_author_and_hides_it_from_get()
    {
        var authorId = Guid.NewGuid();
        var created = await SeedPostAsync(authorId);
        var service = CreateService(metadata: created.Metadata, content: created.Content);

        var deleted = await service.DeleteAsync(
            created.PostId,
            new DeletePostRequest(authorId),
            CancellationToken.None);
        var getAfterDelete = await service.GetAsync(created.PostId, viewerId: null, CancellationToken.None);

        Assert.True(deleted.Succeeded);
        Assert.Equal(PostStatus.Deleted, deleted.Value!.Status);
        Assert.False(getAfterDelete.Succeeded);
        Assert.Equal(404, getAfterDelete.StatusCode);
    }

    [Fact]
    public async Task Moderation_delete_marks_post_deleted_without_author()
    {
        var created = await SeedPostAsync(Guid.NewGuid());
        var service = CreateService(metadata: created.Metadata, content: created.Content);

        var result = await service.DeleteByModeratorAsync(created.PostId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(PostStatus.Deleted, result.Value!.Status);
    }

    [Fact]
    public async Task VoteAsync_updates_score_and_viewer_vote()
    {
        var viewerId = Guid.NewGuid();
        var created = await SeedPostAsync(Guid.NewGuid());
        var votes = new InMemoryVoteRepository();
        var service = CreateService(metadata: created.Metadata, content: created.Content, votes: votes);

        var upvote = await service.VoteAsync(
            created.PostId,
            new VotePostRequest(1),
            viewerId,
            CancellationToken.None);
        var downvote = await service.VoteAsync(
            created.PostId,
            new VotePostRequest(-1),
            viewerId,
            CancellationToken.None);

        Assert.True(upvote.Succeeded);
        Assert.Equal(1, upvote.Value!.Score);
        Assert.True(downvote.Succeeded);
        Assert.Equal(-1, downvote.Value!.Score);
        Assert.Equal(-1, downvote.Value.ViewerVote);
    }

    [Fact]
    public async Task VoteAsync_clears_existing_vote()
    {
        var viewerId = Guid.NewGuid();
        var created = await SeedPostAsync(Guid.NewGuid());
        var votes = new InMemoryVoteRepository();
        var service = CreateService(metadata: created.Metadata, content: created.Content, votes: votes);

        await service.VoteAsync(created.PostId, new VotePostRequest(1), viewerId, CancellationToken.None);
        var cleared = await service.VoteAsync(created.PostId, new VotePostRequest(0), viewerId, CancellationToken.None);

        Assert.True(cleared.Succeeded);
        Assert.Equal(0, cleared.Value!.Score);
        Assert.Equal(0, cleared.Value.ViewerVote);
    }

    [Fact]
    public async Task VoteAsync_rejects_invalid_value()
    {
        var created = await SeedPostAsync(Guid.NewGuid());
        var service = CreateService(metadata: created.Metadata, content: created.Content);

        var result = await service.VoteAsync(
            created.PostId,
            new VotePostRequest(2),
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
    }

    private static PostMediaUploadRequest Media(string fileName, string contentType, string text)
    {
        return new PostMediaUploadRequest(
            fileName,
            contentType,
            Convert.ToBase64String(Encoding.UTF8.GetBytes(text)));
    }

    private static PostMedia NewMedia(Guid postId, string objectKey) =>
        new(Guid.NewGuid(), postId, objectKey, Path.GetFileName(objectKey), "image/jpeg", 10, DateTimeOffset.UtcNow);

    private static async Task<SeededPost> SeedPostAsync(Guid authorId)
    {
        var metadata = new InMemoryMetadataRepository();
        var content = new InMemoryContentRepository();
        var post = PostMetadata.Create(authorId, Guid.NewGuid(), "Original", DateTimeOffset.UtcNow);
        await metadata.AddAsync(post, CancellationToken.None);
        await content.SaveAsync(new PostContent(post.Id, "Original text", DateTimeOffset.UtcNow), CancellationToken.None);
        return new SeededPost(post.Id, metadata, content);
    }

    private static PostService CreateService(
        bool isMember = true,
        bool isOwner = true,
        bool canViewPosts = true,
        InMemoryMetadataRepository? metadata = null,
        InMemoryContentRepository? content = null,
        InMemoryMediaRepository? media = null,
        FakeMediaStorage? storage = null,
        InMemoryVoteRepository? votes = null) =>
        new(
            metadata ?? new InMemoryMetadataRepository(),
            content ?? new InMemoryContentRepository(),
            media ?? new InMemoryMediaRepository(),
            storage ?? new FakeMediaStorage(),
            votes ?? new InMemoryVoteRepository(),
            new FakeCommunityAccessClient(isMember, isOwner, canViewPosts),
            new FixedClock());

    private sealed record SeededPost(
        Guid PostId,
        InMemoryMetadataRepository Metadata,
        InMemoryContentRepository Content);

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

    private sealed class InMemoryMediaRepository : IPostMediaRepository
    {
        public List<PostMedia> Items { get; } = [];

        public Task<PostMedia?> GetByIdAsync(Guid mediaId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.FirstOrDefault(media => media.Id == mediaId));

        public Task<IReadOnlyCollection<PostMedia>> ListByPostIdAsync(Guid postId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<PostMedia>>(Items.Where(media => media.PostId == postId).ToArray());

        public Task AddRangeAsync(IReadOnlyCollection<PostMedia> media, CancellationToken cancellationToken)
        {
            Items.AddRange(media);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeMediaStorage : IPostMediaStorage
    {
        public List<PostMediaUpload> Uploads { get; } = [];
        public Dictionary<string, byte[]> Files { get; } = [];

        public Task<StoredPostMedia> SaveAsync(PostMediaUpload upload, CancellationToken cancellationToken)
        {
            Uploads.Add(upload);
            var objectKey = $"posts/{upload.PostId:N}/{upload.FileName}";
            Files[objectKey] = upload.Content;
            return Task.FromResult(new StoredPostMedia(objectKey, upload.Content.LongLength));
        }

        public Task<byte[]> ReadAsync(string objectKey, CancellationToken cancellationToken) =>
            Task.FromResult(Files[objectKey]);
    }

    private sealed class InMemoryVoteRepository : IPostVoteRepository
    {
        private readonly Dictionary<(Guid PostId, Guid UserId), int> _votes = [];

        public Task<PostVoteTotals> GetTotalsAsync(Guid postId, CancellationToken cancellationToken)
        {
            var postVotes = _votes.Where(vote => vote.Key.PostId == postId).Select(vote => vote.Value).ToArray();
            return Task.FromResult(new PostVoteTotals(
                postVotes.Count(value => value == 1),
                postVotes.Count(value => value == -1)));
        }

        public Task<int> GetUserVoteAsync(Guid postId, Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_votes.TryGetValue((postId, userId), out var value) ? value : 0);
        }

        public Task SetVoteAsync(Guid postId, Guid userId, int value, DateTimeOffset now, CancellationToken cancellationToken)
        {
            _votes[(postId, userId)] = value;
            return Task.CompletedTask;
        }

        public Task ClearVoteAsync(Guid postId, Guid userId, CancellationToken cancellationToken)
        {
            _votes.Remove((postId, userId));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCommunityAccessClient(bool isMember, bool isOwner, bool canViewPosts) : ICommunityAccessClient
    {
        public Task<bool> IsMemberAsync(Guid userId, Guid communityId, CancellationToken cancellationToken) =>
            Task.FromResult(isMember);

        public Task<bool> IsOwnerAsync(Guid userId, Guid communityId, CancellationToken cancellationToken) =>
            Task.FromResult(isOwner);

        public Task<bool> CanViewPostsAsync(Guid? userId, Guid communityId, CancellationToken cancellationToken) =>
            Task.FromResult(canViewPosts);
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);
    }
}
