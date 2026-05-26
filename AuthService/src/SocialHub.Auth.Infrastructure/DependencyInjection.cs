using SocialHub.Auth.Application.Abstractions;
using SocialHub.Auth.Infrastructure.Persistence;
using SocialHub.Auth.Infrastructure.Persistence.Repositories;
using SocialHub.Auth.Infrastructure.Security;
using SocialHub.Auth.Infrastructure.External;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace SocialHub.Auth.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AuthDb")
            ?? throw new InvalidOperationException("Connection string 'AuthDb' is not configured.");

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddDbContext<AuthDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAuthSessionRepository, AuthSessionRepository>();
        services.AddScoped<ILoginAuditRepository, LoginAuditRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, TokenService>();
        services.Configure<ExternalServiceOptions>(configuration.GetSection(ExternalServiceOptions.SectionName));
        services.AddHttpClient<INotificationClient, NotificationClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ExternalServiceOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(options.NotificationBaseUrl))
            {
                client.BaseAddress = new Uri(options.NotificationBaseUrl);
            }

            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
            if (!string.IsNullOrWhiteSpace(options.InternalToken))
            {
                client.DefaultRequestHeaders.Add("X-Internal-Token", options.InternalToken);
            }
        });

        return services;
    }
}
