namespace SocialHub.Post.Infrastructure.Persistence;

public sealed class MinioOptions
{
    public const string SectionName = "Minio";

    public string Endpoint { get; set; } = "http://localhost:9000";
    public string AccessKey { get; set; } = "local_minio_user";
    public string SecretKey { get; set; } = "local_minio_password";
    public string BucketName { get; set; } = "post-contents";
}
