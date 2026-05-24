namespace SocialHub.Post.Domain;

public sealed class PostMedia
{
    public PostMedia(
        Guid id,
        Guid postId,
        string objectKey,
        string fileName,
        string contentType,
        long size,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("Media id is required.");
        }

        if (postId == Guid.Empty)
        {
            throw new DomainException("Post id is required.");
        }

        if (string.IsNullOrWhiteSpace(objectKey))
        {
            throw new DomainException("Media object key is required.");
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new DomainException("Media file name is required.");
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new DomainException("Media content type is required.");
        }

        if (size <= 0)
        {
            throw new DomainException("Media file must not be empty.");
        }

        Id = id;
        PostId = postId;
        ObjectKey = objectKey.Trim();
        FileName = fileName.Trim();
        ContentType = contentType.Trim();
        Size = size;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }
    public Guid PostId { get; }
    public string ObjectKey { get; }
    public string FileName { get; }
    public string ContentType { get; }
    public long Size { get; }
    public DateTimeOffset CreatedAt { get; }
}
