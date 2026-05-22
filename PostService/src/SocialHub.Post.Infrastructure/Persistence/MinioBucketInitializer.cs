using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace SocialHub.Post.Infrastructure.Persistence;

public sealed class MinioBucketInitializer : IHostedService
{
    private readonly IAmazonS3 _s3;
    private readonly MinioOptions _options;

    public MinioBucketInitializer(IAmazonS3 s3, IOptions<MinioOptions> options)
    {
        _s3 = s3;
        _options = options.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var buckets = await _s3.ListBucketsAsync(cancellationToken);
        if (buckets.Buckets.Any(bucket => bucket.BucketName == _options.BucketName))
        {
            return;
        }

        await _s3.PutBucketAsync(new PutBucketRequest
        {
            BucketName = _options.BucketName,
            UseClientRegion = true
        }, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
