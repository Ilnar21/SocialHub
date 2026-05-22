namespace SocialHub.Post.Domain;

public sealed class PostMetadata
{
    public Guid Id { get; init; }
    public Guid AuthorId { get; init; }
    public Guid CommunityId { get; init; }
    public string Title { get; private set; }
    public PostStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    public PostMetadata(
        Guid id,
        Guid authorId,
        Guid communityId,
        string title,
        PostStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset? updatedAt,
        DateTimeOffset? deletedAt)
    {
        Id = id;
        AuthorId = authorId;
        CommunityId = communityId;
        Title = title;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        DeletedAt = deletedAt;
    }

    private PostMetadata(
        Guid id,
        Guid authorId,
        Guid communityId,
        string title,
        DateTimeOffset createdAt)
        : this(id, authorId, communityId, title, PostStatus.Published, createdAt, null, null)
    {
    }

    public static PostMetadata Create(Guid authorId, Guid communityId, string title, DateTimeOffset now)
    {
        if (authorId == Guid.Empty)
        {
            throw new DomainException("Author is required.");
        }

        if (communityId == Guid.Empty)
        {
            throw new DomainException("Публикации доступны только в сообществах");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("Title is required.");
        }

        return new PostMetadata(Guid.NewGuid(), authorId, communityId, title.Trim(), now);
    }

    public void Rename(string title, DateTimeOffset now)
    {
        if (Status == PostStatus.Deleted)
        {
            throw new DomainException("Deleted post cannot be edited.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("Title is required.");
        }

        Title = title.Trim();
        UpdatedAt = now;
    }

    public void MarkDeleted(Guid actorId, DateTimeOffset now)
    {
        if (actorId != AuthorId)
        {
            throw new DomainException("Only author can delete post.");
        }

        Status = PostStatus.Deleted;
        DeletedAt = now;
        UpdatedAt = now;
    }
}
