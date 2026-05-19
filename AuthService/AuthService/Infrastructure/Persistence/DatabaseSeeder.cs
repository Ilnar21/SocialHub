using AuthService.Application.Abstractions;
using AuthService.Domain.Entities;
using AuthService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        await SeedUserAsync(dbContext, passwordHasher, "ivan.petrov", "ivan@test.local", "Passw0rd!", "Ivan Petrov", UserRole.User, cancellationToken);
        await SeedUserAsync(dbContext, passwordHasher, "maria.sokolova", "maria@test.local", "Passw0rd!", "Maria Sokolova", UserRole.User, cancellationToken);
        await SeedUserAsync(dbContext, passwordHasher, "anna.admin", "anna@test.local", "Passw0rd!", "Anna Belova", UserRole.CommunityAdmin, cancellationToken);
        await SeedUserAsync(dbContext, passwordHasher, "pavel.mod", "pavel@test.local", "Passw0rd!", "Pavel Moderatorov", UserRole.PlatformModerator, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedUserAsync(
        AuthDbContext dbContext,
        IPasswordHasher passwordHasher,
        string username,
        string email,
        string password,
        string displayName,
        UserRole role,
        CancellationToken cancellationToken)
    {
        if (await dbContext.Users.AnyAsync(user => user.Username == username, cancellationToken))
        {
            return;
        }

        await dbContext.Users.AddAsync(new UserAccount
        {
            Username = username,
            Email = email,
            PasswordHash = passwordHasher.Hash(password),
            Role = role,
            Profile = { DisplayName = displayName }
        }, cancellationToken);
    }
}
