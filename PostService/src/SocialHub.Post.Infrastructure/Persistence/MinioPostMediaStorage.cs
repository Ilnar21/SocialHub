using Amazon.S3;
using Amazon.S3.Model;
using SocialHub.Post.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace SocialHub.Post.Infrastructure.Persistence;

public sealed class MinioPostMediaStorage(IAmazonS3 s3, IOptions<MinioOptions> options) : IPostMediaStorage
{
    public async Task<StoredPostMedia> SaveAsync(PostMediaUpload upload, CancellationToken cancellationToken)
    {
        var objectKey = $"posts/{upload.PostId:N}/{Guid.NewGuid():N}-{Sanitize(upload.FileName)}";
        await using var stream = new MemoryStream(upload.Content);

        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = options.Value.BucketName,
            Key = objectKey,
            InputStream = stream,
            ContentType = upload.ContentType
        }, cancellationToken);

        return new StoredPostMedia(objectKey, upload.Content.LongLength);
    }

    public async Task<byte[]> ReadAsync(string objectKey, CancellationToken cancellationToken)
    {
        using var response = await s3.GetObjectAsync(options.Value.BucketName, objectKey, cancellationToken);
        await using var responseStream = response.ResponseStream;
        using var memory = new MemoryStream();
        await responseStream.CopyToAsync(memory, cancellationToken);
        return memory.ToArray();
    }

    private static string Sanitize(string fileName)
    {
        var name = Path.GetFileName(fileName.Trim());
        var chars = name.Select(character =>
            char.IsLetterOrDigit(character) || character is '.' or '-' or '_'
                ? character
                : '-').ToArray();

        var sanitized = new string(chars).Trim('-', '.', '_');
        return string.IsNullOrWhiteSpace(sanitized) ? "media" : sanitized;
    }
}
