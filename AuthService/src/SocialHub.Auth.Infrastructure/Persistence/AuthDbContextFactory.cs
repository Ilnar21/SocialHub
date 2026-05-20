using SocialHub.Auth.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SocialHub.Auth.Infrastructure.Persistence;

public sealed class AuthDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    public AuthDbContext CreateDbContext(string[] args)
    {
        DotEnv.Load();
        PostgresEnvironment.ApplyConnectionString();

        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__AuthDb")
            ?? "Host=localhost;Port=5432;Database=socialhub_auth;Username=postgres;Password=1";

        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AuthDbContext(options);
    }
}
