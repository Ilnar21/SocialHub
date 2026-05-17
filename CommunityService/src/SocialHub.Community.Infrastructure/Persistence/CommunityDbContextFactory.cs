using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SocialHub.Community.Infrastructure.Persistence;

public sealed class CommunityDbContextFactory : IDesignTimeDbContextFactory<CommunityDbContext>
{
    public CommunityDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CommunityDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5433;Database=socialhub_community;Username=socialhub;Password=socialhub_password");

        return new CommunityDbContext(optionsBuilder.Options);
    }
}
