namespace SocialHub.Post.Application.Abstractions;

public interface IPostMediaStorage
{
    Task<StoredPostMedia> SaveAsync(PostMediaUpload upload, CancellationToken cancellationToken);
}

public sealed record PostMediaUpload(
    Guid PostId,
    string FileName,
    string ContentType,
    byte[] Content);

public sealed record StoredPostMedia(string ObjectKey, long Size);
