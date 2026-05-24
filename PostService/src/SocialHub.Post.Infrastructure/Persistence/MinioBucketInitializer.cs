using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace SocialHub.Post.Infrastructure.Persistence;

public sealed class MinioBucketInitializer(IAmazonS3 s3, IOptions<MinioOptions> options) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var bucketName = options.Value.BucketName;
        var buckets = await s3.ListBucketsAsync(cancellationToken);
        if (buckets.Buckets.Any(bucket => bucket.BucketName == bucketName))
        {
            return;
        }

        await s3.PutBucketAsync(new PutBucketRequest
        {
            BucketName = bucketName,
            UseClientRegion = true
        }, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
