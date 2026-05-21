using Microsoft.Extensions.DependencyInjection;
using SocialHub.Post.Application.Posts;

namespace SocialHub.Post.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddPostApplication(this IServiceCollection services)
    {
        services.AddScoped<PostService>();
        return services;
    }
}
