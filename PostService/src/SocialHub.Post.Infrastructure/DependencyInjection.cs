using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        services.AddSingleton<IPostMetadataRepository, InMemoryPostMetadataRepository>();
        services.AddSingleton<IPostContentRepository, InMemoryPostContentRepository>();
        services.AddHttpClient<ICommunityAccessClient, CommunityAccessClient>(client =>
        {
            client.BaseAddress = new Uri(communityOptions.BaseUrl);
        });

        return services;
    }
}
