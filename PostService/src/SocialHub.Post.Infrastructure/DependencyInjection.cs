using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Amazon.Runtime;
using Amazon.S3;
using Npgsql;
using SocialHub.Post.Application.Abstractions;
using SocialHub.Post.Infrastructure.Community;
using SocialHub.Post.Infrastructure.Persistence;

namespace SocialHub.Post.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPostInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var communityOptions = configuration
            .GetSection("CommunityService")
            .Get<CommunityAccessOptions>() ?? new CommunityAccessOptions();

        services.AddSingleton(communityOptions);
        services.AddSingleton<IClock, SystemClock>();
        services.Configure<PostgresOptions>(options =>
        {
            options.ConnectionString = configuration["POSTGRES_CONNECTION_STRING"]
                ?? configuration.GetConnectionString("Postgres")
                ?? throw new InvalidOperationException("ConnectionStrings__Postgres is required.");
        });
        services.Configure<MinioOptions>(options =>
        {
            configuration.GetSection(MinioOptions.SectionName).Bind(options);

            options.Endpoint = FirstConfigured(configuration["MINIO_ENDPOINT"], options.Endpoint);
            options.AccessKey = FirstConfigured(configuration["MINIO_ACCESS_KEY"], options.AccessKey);
            options.SecretKey = FirstConfigured(configuration["MINIO_SECRET_KEY"], options.SecretKey);
            options.BucketName = FirstConfigured(configuration["MINIO_BUCKET"], options.BucketName);
        });

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<PostgresOptions>>().Value;
            return NpgsqlDataSource.Create(options.ConnectionString);
        });
        services.AddSingleton<IAmazonS3>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MinioOptions>>().Value;
            var credentials = new BasicAWSCredentials(options.AccessKey, options.SecretKey);
            var config = new AmazonS3Config
            {
                ServiceURL = options.Endpoint,
                ForcePathStyle = true,
                UseHttp = options.Endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            };

            return new AmazonS3Client(credentials, config);
        });

        services.AddHostedService<PostgresDatabaseInitializer>();
        services.AddHostedService<MinioBucketInitializer>();
        services.AddSingleton<IPostMetadataRepository, PostgresPostMetadataRepository>();
        services.AddSingleton<IPostContentRepository, MinioPostContentRepository>();
        services.AddHttpClient<ICommunityAccessClient, CommunityAccessClient>(client =>
        {
            client.BaseAddress = new Uri(communityOptions.BaseUrl);
            if (!string.IsNullOrWhiteSpace(communityOptions.InternalToken))
            {
                client.DefaultRequestHeaders.Add("X-Internal-Token", communityOptions.InternalToken);
            }
        });

        return services;
    }

    private static string FirstConfigured(string? preferred, string currentValue)
    {
        return !string.IsNullOrWhiteSpace(preferred)
            ? preferred
            : currentValue;
    }
}
