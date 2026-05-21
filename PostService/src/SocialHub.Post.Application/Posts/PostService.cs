using SocialHub.Post.Application.Abstractions;
using SocialHub.Post.Domain;

namespace SocialHub.Post.Application.Posts;

public sealed class PostService
{
    public const int DailyPostLimit = 100;

    private readonly IPostMetadataRepository _metadataRepository;
    private readonly IPostContentRepository _contentRepository;
    private readonly ICommunityAccessClient _communityAccessClient;
    private readonly IClock _clock;

    public PostService(
        IPostMetadataRepository metadataRepository,
        IPostContentRepository contentRepository,
        ICommunityAccessClient communityAccessClient,
        IClock clock)
    {
        _metadataRepository = metadataRepository;
        _contentRepository = contentRepository;
        _communityAccessClient = communityAccessClient;
        _clock = clock;
    }

    public async Task<OperationResult<PostResponse>> CreateAsync(
        CreatePostRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CommunityId == Guid.Empty)
        {
            return OperationResult<PostResponse>.Fail("Публикации доступны только в сообществах", 400);
        }

        if (!await _communityAccessClient.IsMemberAsync(request.AuthorId, request.CommunityId, cancellationToken))
        {
            return OperationResult<PostResponse>.Fail("Вы не состоите в этом сообществе", 403);
        }

        var since = _clock.UtcNow.AddHours(-24);
        var publishedCount = await _metadataRepository.CountPublishedByAuthorInCommunitySinceAsync(
            request.AuthorId,
            request.CommunityId,
            since,
            cancellationToken);

        if (publishedCount >= DailyPostLimit)
        {
            return OperationResult<PostResponse>.Fail("Достигнут суточный лимит: 100 постов", 429);
        }

        try
        {
            var now = _clock.UtcNow;
            var metadata = PostMetadata.Create(request.AuthorId, request.CommunityId, request.Title, now);
            var content = new PostContent(metadata.Id, request.Text, now);

            await _metadataRepository.AddAsync(metadata, cancellationToken);
            await _contentRepository.SaveAsync(content, cancellationToken);

            return OperationResult<PostResponse>.Created(ToResponse(metadata, content));
        }
        catch (DomainException exception)
        {
            return OperationResult<PostResponse>.Fail(exception.Message, 400);
        }
    }

    public async Task<OperationResult<PostResponse>> GetAsync(Guid postId, CancellationToken cancellationToken)
    {
        var metadata = await _metadataRepository.GetByIdAsync(postId, cancellationToken);
        if (metadata is null || metadata.Status == PostStatus.Deleted)
        {
            return OperationResult<PostResponse>.Fail("Post not found.", 404);
        }

        var content = await _contentRepository.GetByPostIdAsync(postId, cancellationToken);
        if (content is null)
        {
            return OperationResult<PostResponse>.Fail("Post content not found.", 404);
        }

        return OperationResult<PostResponse>.Ok(ToResponse(metadata, content));
    }

    public async Task<IReadOnlyCollection<PostResponse>> ListByCommunityAsync(
        Guid communityId,
        CancellationToken cancellationToken)
    {
        var posts = await _metadataRepository.ListByCommunityAsync(communityId, cancellationToken);
        var result = new List<PostResponse>();

        foreach (var metadata in posts.Where(post => post.Status == PostStatus.Published))
        {
            var content = await _contentRepository.GetByPostIdAsync(metadata.Id, cancellationToken);
            if (content is not null)
            {
                result.Add(ToResponse(metadata, content));
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

            return OperationResult<PostResponse>.Ok(ToResponse(metadata, content));
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
            metadata.MarkDeleted(request.ActorId, _clock.UtcNow);
            await _metadataRepository.UpdateAsync(metadata, cancellationToken);

            return OperationResult<PostResponse>.Ok(ToResponse(metadata, content));
        }
        catch (DomainException exception)
        {
            return OperationResult<PostResponse>.Fail(exception.Message, 403);
        }
    }

    private static PostResponse ToResponse(PostMetadata metadata, PostContent content)
    {
        return new PostResponse(
            metadata.Id,
            metadata.AuthorId,
            metadata.CommunityId,
            metadata.Title,
            content.Text,
            metadata.Status,
            metadata.CreatedAt,
            metadata.UpdatedAt);
    }
}
