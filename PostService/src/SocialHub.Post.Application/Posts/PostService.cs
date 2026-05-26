using SocialHub.Post.Application.Abstractions;
using SocialHub.Post.Domain;

namespace SocialHub.Post.Application.Posts;

public sealed class PostService
{
    public const int DailyPostLimit = 100;
    public const int MaxMediaFiles = 10;
    public const int MaxMediaFileBytes = 10 * 1024 * 1024;

    private readonly IPostMetadataRepository _metadataRepository;
    private readonly IPostContentRepository _contentRepository;
    private readonly IPostMediaRepository _mediaRepository;
    private readonly IPostMediaStorage _mediaStorage;
    private readonly IPostVoteRepository _voteRepository;
    private readonly ICommunityAccessClient _communityAccessClient;
    private readonly IClock _clock;

    public PostService(
        IPostMetadataRepository metadataRepository,
        IPostContentRepository contentRepository,
        IPostMediaRepository mediaRepository,
        IPostMediaStorage mediaStorage,
        IPostVoteRepository voteRepository,
        ICommunityAccessClient communityAccessClient,
        IClock clock)
    {
        _metadataRepository = metadataRepository;
        _contentRepository = contentRepository;
        _mediaRepository = mediaRepository;
        _mediaStorage = mediaStorage;
        _voteRepository = voteRepository;
        _communityAccessClient = communityAccessClient;
        _clock = clock;
    }

    public async Task<OperationResult<PostResponse>> CreateAsync(
        CreatePostRequest request,
        CancellationToken cancellationToken)
    {
        return await CreateCoreAsync(request, requireCommunityOwner: true, cancellationToken);
    }

    public async Task<OperationResult<PostResponse>> CreateApprovedSuggestedAsync(
        CreatePostRequest request,
        CancellationToken cancellationToken)
    {
        return await CreateCoreAsync(request, requireCommunityOwner: false, cancellationToken);
    }

    private async Task<OperationResult<PostResponse>> CreateCoreAsync(
        CreatePostRequest request,
        bool requireCommunityOwner,
        CancellationToken cancellationToken)
    {
        if (request.CommunityId == Guid.Empty)
        {
            return OperationResult<PostResponse>.Fail("Publications are available only inside communities.", 400);
        }

        var hasCommunityAccess = requireCommunityOwner
            ? await _communityAccessClient.IsOwnerAsync(request.AuthorId, request.CommunityId, cancellationToken)
            : await _communityAccessClient.IsMemberAsync(request.AuthorId, request.CommunityId, cancellationToken);

        if (!hasCommunityAccess)
        {
            var message = requireCommunityOwner
                ? "Only community owner can publish posts directly."
                : "Author is not a member of the community.";

            return OperationResult<PostResponse>.Fail(message, 403);
        }

        var since = _clock.UtcNow.AddHours(-24);
        var publishedCount = await _metadataRepository.CountPublishedByAuthorInCommunitySinceAsync(
            request.AuthorId,
            request.CommunityId,
            since,
            cancellationToken);

        if (publishedCount >= DailyPostLimit)
        {
            return OperationResult<PostResponse>.Fail("Daily post limit reached.", 429);
        }

        try
        {
            var now = _clock.UtcNow;
            var metadata = PostMetadata.Create(request.AuthorId, request.CommunityId, request.Title, now);
            var content = new PostContent(metadata.Id, request.Text, now);
            var media = await SaveMediaAsync(metadata.Id, request.Media, now, cancellationToken);

            await _metadataRepository.AddAsync(metadata, cancellationToken);
            await _contentRepository.SaveAsync(content, cancellationToken);
            await _mediaRepository.AddRangeAsync(media, cancellationToken);

            return OperationResult<PostResponse>.Created(await ToResponseAsync(
                metadata,
                content,
                media,
                request.AuthorId,
                cancellationToken));
        }
        catch (DomainException exception)
        {
            return OperationResult<PostResponse>.Fail(exception.Message, 400);
        }
        catch (FormatException)
        {
            return OperationResult<PostResponse>.Fail("Media content must be valid base64.", 400);
        }
    }

    public async Task<OperationResult<PostResponse>> GetAsync(
        Guid postId,
        Guid? viewerId,
        CancellationToken cancellationToken,
        bool skipVisibilityCheck = false)
    {
        var metadata = await _metadataRepository.GetByIdAsync(postId, cancellationToken);
        if (metadata is null || metadata.Status == PostStatus.Deleted)
        {
            return OperationResult<PostResponse>.Fail("Post not found.", 404);
        }

        if (!skipVisibilityCheck && !await CanViewPostAsync(metadata, viewerId, cancellationToken))
        {
            return OperationResult<PostResponse>.Fail("Post is available only to approved community members.", 403);
        }

        var post = await LoadPostAsync(metadata, viewerId, cancellationToken);
        return post is null
            ? OperationResult<PostResponse>.Fail("Post not found.", 404)
            : OperationResult<PostResponse>.Ok(post);
    }

    public async Task<OperationResult<PostMediaDownloadResponse>> GetMediaAsync(
        Guid postId,
        Guid mediaId,
        CancellationToken cancellationToken)
    {
        var metadata = await _metadataRepository.GetByIdAsync(postId, cancellationToken);
        if (metadata is null || metadata.Status == PostStatus.Deleted)
        {
            return OperationResult<PostMediaDownloadResponse>.Fail("Post not found.", 404);
        }

        var media = await _mediaRepository.GetByIdAsync(mediaId, cancellationToken);
        if (media is null || media.PostId != postId)
        {
            return OperationResult<PostMediaDownloadResponse>.Fail("Media not found.", 404);
        }

        var content = await _mediaStorage.ReadAsync(media.ObjectKey, cancellationToken);
        return OperationResult<PostMediaDownloadResponse>.Ok(new PostMediaDownloadResponse(
            media.FileName,
            media.ContentType,
            content));
    }

    public async Task<IReadOnlyCollection<PostResponse>> ListByCommunityAsync(
        Guid communityId,
        Guid? viewerId,
        CancellationToken cancellationToken,
        bool skipVisibilityCheck = false)
    {
        if (!skipVisibilityCheck
            && !await _communityAccessClient.CanViewPostsAsync(viewerId, communityId, cancellationToken))
        {
            return [];
        }

        var posts = await _metadataRepository.ListByCommunityAsync(communityId, cancellationToken);
        var result = new List<PostResponse>();

        foreach (var metadata in posts.Where(post => post.Status == PostStatus.Published))
        {
            var post = await LoadPostAsync(metadata, viewerId, cancellationToken);
            if (post is not null)
            {
                result.Add(post);
            }
        }

        return result
            .OrderByDescending(post => post.CreatedAt)
            .ToArray();
    }

    public async Task<OperationResult<PostResponse>> UpdateAsync(
        Guid postId,
        UpdatePostRequest request,
        CancellationToken cancellationToken)
    {
        var metadata = await _metadataRepository.GetByIdAsync(postId, cancellationToken);
        var content = await _contentRepository.GetByPostIdAsync(postId, cancellationToken);

        if (metadata is null || content is null || metadata.Status == PostStatus.Deleted)
        {
            return OperationResult<PostResponse>.Fail("Post not found.", 404);
        }

        if (metadata.AuthorId != request.ActorId)
        {
            return OperationResult<PostResponse>.Fail("Only author can edit post.", 403);
        }

        if (!await _communityAccessClient.IsOwnerAsync(request.ActorId, metadata.CommunityId, cancellationToken))
        {
            return OperationResult<PostResponse>.Fail("Only community owner can edit posts directly.", 403);
        }

        try
        {
            var now = _clock.UtcNow;
            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                metadata.Rename(request.Title, now);
            }

            content.ChangeText(request.Text, now);
            await _metadataRepository.UpdateAsync(metadata, cancellationToken);
            await _contentRepository.UpdateAsync(content, cancellationToken);

            var media = await _mediaRepository.ListByPostIdAsync(postId, cancellationToken);
            return OperationResult<PostResponse>.Ok(await ToResponseAsync(
                metadata,
                content,
                media,
                request.ActorId,
                cancellationToken));
        }
        catch (DomainException exception)
        {
            return OperationResult<PostResponse>.Fail(exception.Message, 400);
        }
    }

    public async Task<OperationResult<PostResponse>> DeleteAsync(
        Guid postId,
        DeletePostRequest request,
        CancellationToken cancellationToken)
    {
        var metadata = await _metadataRepository.GetByIdAsync(postId, cancellationToken);
        var content = await _contentRepository.GetByPostIdAsync(postId, cancellationToken);

        if (metadata is null || content is null || metadata.Status == PostStatus.Deleted)
        {
            return OperationResult<PostResponse>.Fail("Post not found.", 404);
        }

        try
        {
            if (metadata.AuthorId == request.ActorId)
            {
                metadata.MarkDeleted(request.ActorId, _clock.UtcNow);
            }
            else if (await _communityAccessClient.IsOwnerAsync(request.ActorId, metadata.CommunityId, cancellationToken))
            {
                metadata.MarkDeletedByCommunityOwner(_clock.UtcNow);
            }
            else
            {
                return OperationResult<PostResponse>.Fail("Only author or community owner can delete post.", 403);
            }

            await _metadataRepository.UpdateAsync(metadata, cancellationToken);

            var media = await _mediaRepository.ListByPostIdAsync(postId, cancellationToken);
            return OperationResult<PostResponse>.Ok(await ToResponseAsync(
                metadata,
                content,
                media,
                request.ActorId,
                cancellationToken));
        }
        catch (DomainException exception)
        {
            return OperationResult<PostResponse>.Fail(exception.Message, 403);
        }
    }

    public async Task<OperationResult<PostResponse>> DeleteByModeratorAsync(
        Guid postId,
        CancellationToken cancellationToken)
    {
        var metadata = await _metadataRepository.GetByIdAsync(postId, cancellationToken);
        var content = await _contentRepository.GetByPostIdAsync(postId, cancellationToken);

        if (metadata is null || content is null || metadata.Status == PostStatus.Deleted)
        {
            return OperationResult<PostResponse>.Fail("Post not found.", 404);
        }

        metadata.MarkDeletedByModerator(_clock.UtcNow);
        await _metadataRepository.UpdateAsync(metadata, cancellationToken);

        var media = await _mediaRepository.ListByPostIdAsync(postId, cancellationToken);
        return OperationResult<PostResponse>.Ok(await ToResponseAsync(
            metadata,
            content,
            media,
            viewerId: null,
            cancellationToken));
    }

    public async Task<OperationResult<PostVoteResponse>> VoteAsync(
        Guid postId,
        VotePostRequest request,
        Guid viewerId,
        CancellationToken cancellationToken)
    {
        if (request.Value is not (-1 or 0 or 1))
        {
            return OperationResult<PostVoteResponse>.Fail("Vote value must be -1, 0 or 1.", 400);
        }

        var metadata = await _metadataRepository.GetByIdAsync(postId, cancellationToken);
        if (metadata is null || metadata.Status == PostStatus.Deleted)
        {
            return OperationResult<PostVoteResponse>.Fail("Post not found.", 404);
        }

        if (!await CanViewPostAsync(metadata, viewerId, cancellationToken))
        {
            return OperationResult<PostVoteResponse>.Fail("Post is available only to approved community members.", 403);
        }

        if (request.Value == 0)
        {
            await _voteRepository.ClearVoteAsync(postId, viewerId, cancellationToken);
        }
        else
        {
            await _voteRepository.SetVoteAsync(postId, viewerId, request.Value, _clock.UtcNow, cancellationToken);
        }

        var totals = await _voteRepository.GetTotalsAsync(postId, cancellationToken);
        var viewerVote = await _voteRepository.GetUserVoteAsync(postId, viewerId, cancellationToken);
        return OperationResult<PostVoteResponse>.Ok(new PostVoteResponse(
            postId,
            totals.Upvotes,
            totals.Downvotes,
            totals.Score,
            viewerVote));
    }

    private async Task<PostResponse?> LoadPostAsync(
        Guid postId,
        Guid? viewerId,
        CancellationToken cancellationToken)
    {
        var metadata = await _metadataRepository.GetByIdAsync(postId, cancellationToken);
        if (metadata is null || metadata.Status == PostStatus.Deleted)
        {
            return null;
        }

        return await LoadPostAsync(metadata, viewerId, cancellationToken);
    }

    private async Task<PostResponse?> LoadPostAsync(
        PostMetadata metadata,
        Guid? viewerId,
        CancellationToken cancellationToken)
    {
        var content = await _contentRepository.GetByPostIdAsync(metadata.Id, cancellationToken);
        if (content is null)
        {
            return null;
        }

        var media = await _mediaRepository.ListByPostIdAsync(metadata.Id, cancellationToken);
        return await ToResponseAsync(metadata, content, media, viewerId, cancellationToken);
    }

    private async Task<bool> CanViewPostAsync(
        PostMetadata metadata,
        Guid? viewerId,
        CancellationToken cancellationToken)
    {
        return await _communityAccessClient.CanViewPostsAsync(viewerId, metadata.CommunityId, cancellationToken);
    }

    private async Task<IReadOnlyCollection<PostMedia>> SaveMediaAsync(
        Guid postId,
        IReadOnlyCollection<PostMediaUploadRequest>? mediaRequests,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (mediaRequests is null || mediaRequests.Count == 0)
        {
            return [];
        }

        if (mediaRequests.Count > MaxMediaFiles)
        {
            throw new DomainException($"A post can contain at most {MaxMediaFiles} media files.");
        }

        var media = new List<PostMedia>(mediaRequests.Count);
        foreach (var request in mediaRequests)
        {
            if (string.IsNullOrWhiteSpace(request.FileName))
            {
                throw new DomainException("Media file name is required.");
            }

            if (string.IsNullOrWhiteSpace(request.ContentType))
            {
                throw new DomainException("Media content type is required.");
            }

            var bytes = Convert.FromBase64String(request.Base64Content);
            if (bytes.Length == 0)
            {
                throw new DomainException("Media file must not be empty.");
            }

            if (bytes.Length > MaxMediaFileBytes)
            {
                throw new DomainException("Media file is too large.");
            }

            var stored = await _mediaStorage.SaveAsync(
                new PostMediaUpload(postId, request.FileName, request.ContentType, bytes),
                cancellationToken);

            media.Add(new PostMedia(
                Guid.NewGuid(),
                postId,
                stored.ObjectKey,
                request.FileName,
                request.ContentType,
                stored.Size,
                now));
        }

        return media;
    }

    private async Task<PostResponse> ToResponseAsync(
        PostMetadata metadata,
        PostContent content,
        IReadOnlyCollection<PostMedia> media,
        Guid? viewerId,
        CancellationToken cancellationToken)
    {
        var totals = await _voteRepository.GetTotalsAsync(metadata.Id, cancellationToken);
        var viewerVote = viewerId.HasValue
            ? await _voteRepository.GetUserVoteAsync(metadata.Id, viewerId.Value, cancellationToken)
            : 0;

        return new PostResponse(
            metadata.Id,
            metadata.AuthorId,
            metadata.CommunityId,
            metadata.Title,
            content.Text,
            metadata.Status,
            metadata.CreatedAt,
            metadata.UpdatedAt,
            totals.Upvotes,
            totals.Downvotes,
            totals.Score,
            viewerVote,
            media.Select(ToResponse).ToArray());
    }

    private static PostMediaResponse ToResponse(PostMedia media)
    {
        return new PostMediaResponse(
            media.Id,
            media.FileName,
            media.ContentType,
            media.Size,
            media.ObjectKey);
    }
}
