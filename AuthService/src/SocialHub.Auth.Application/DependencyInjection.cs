using SocialHub.Auth.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace SocialHub.Auth.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthUserService>();
        return services;
    }
}
