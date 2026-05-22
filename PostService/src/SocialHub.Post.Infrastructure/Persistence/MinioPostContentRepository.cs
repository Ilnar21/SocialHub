using System.Net;
using System.Text;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using SocialHub.Post.Application.Abstractions;
using SocialHub.Post.Domain;

namespace SocialHub.Post.Infrastructure.Persistence;

public sealed class MinioPostContentRepository : IPostContentRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAmazonS3 _s3;
    private readonly MinioOptions _options;

    public MinioPostContentRepository(IAmazonS3 s3, IOptions<MinioOptions> options)
    {
        _s3 = s3;
        _options = options.Value;
    }

    public async Task<PostContent?> GetByPostIdAsync(Guid postId, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _s3.GetObjectAsync(_options.BucketName, GetObjectKey(postId), cancellationToken);
            var document = await JsonSerializer.DeserializeAsync<PostContentObject>(
                response.ResponseStream,
                JsonOptions,
                cancellationToken);

            return document is null
                ? null
                : new PostContent(document.PostId, document.Text, document.UpdatedAt);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task SaveAsync(PostContent content, CancellationToken cancellationToken)
    {
        return PutAsync(content, cancellationToken);
    }

    public Task UpdateAsync(PostContent content, CancellationToken cancellationToken)
    {
        return PutAsync(content, cancellationToken);
    }

    private async Task PutAsync(PostContent content, CancellationToken cancellationToken)
    {
        var document = new PostContentObject(content.PostId, content.Text, content.UpdatedAt);
        var json = JsonSerializer.Serialize(document, JsonOptions);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = GetObjectKey(content.PostId),
            InputStream = stream,
            ContentType = "application/json"
        }, cancellationToken);
    }

    private static string GetObjectKey(Guid postId)
    {
        return $"posts/{postId:N}.json";
    }

    private sealed record PostContentObject(Guid PostId, string Text, DateTimeOffset UpdatedAt);
}
