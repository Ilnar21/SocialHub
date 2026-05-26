using SocialHub.Auth.Application.Abstractions;
using SocialHub.Auth.Application.Services;
using SocialHub.Auth.Domain.Entities;
using SocialHub.Auth.Domain.Enums;

namespace SocialHub.Auth.Application.Tests;

public sealed class PlatformModeratorBootstrapServiceTests
{
    [Fact]
    public async Task ApplyAsync_promotes_existing_users_when_username_and_email_match()
    {
        var moderator = NewUser("moderator", "moderator@example.com");
        var secondModerator = NewUser("second", "second@example.com");
        var unitOfWork = new FakeUnitOfWork();
        var service = new PlatformModeratorBootstrapService(
            new FakeUserRepository(moderator, secondModerator),
            unitOfWork);

        var result = await service.ApplyAsync(
            "moderator:moderator@example.com, second:SECOND@example.com",
            CancellationToken.None);

        Assert.Equal(2, result.PromotedCount);
        Assert.Equal(0, result.AlreadyModeratorCount);
        Assert.Empty(result.SkippedEntries);
        Assert.Equal(UserRole.PlatformModerator, moderator.Role);
        Assert.Equal(UserRole.PlatformModerator, secondModerator.Role);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ApplyAsync_skips_user_when_email_does_not_match()
    {
        var user = NewUser("moderator", "real@example.com");
        var unitOfWork = new FakeUnitOfWork();
        var service = new PlatformModeratorBootstrapService(
            new FakeUserRepository(user),
            unitOfWork);

        var result = await service.ApplyAsync(
            "moderator:attacker@example.com",
            CancellationToken.None);

        Assert.Equal(0, result.PromotedCount);
        Assert.Single(result.SkippedEntries);
        Assert.Equal(UserRole.User, user.Role);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ApplyAsync_is_idempotent_for_existing_platform_moderator()
    {
        var moderator = NewUser("moderator", "moderator@example.com", UserRole.PlatformModerator);
        var unitOfWork = new FakeUnitOfWork();
        var service = new PlatformModeratorBootstrapService(
            new FakeUserRepository(moderator),
            unitOfWork);

        var result = await service.ApplyAsync(
            "moderator:moderator@example.com",
            CancellationToken.None);

        Assert.Equal(0, result.PromotedCount);
        Assert.Equal(1, result.AlreadyModeratorCount);
        Assert.Empty(result.SkippedEntries);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    private static UserAccount NewUser(string username, string email, UserRole role = UserRole.User) =>
        new()
        {
            Username = username,
            Email = email,
            PasswordHash = "hashed:Password123!",
            Role = role,
            Profile = { DisplayName = username }
        };

    private sealed class FakeUserRepository : IUserRepository
    {
        public FakeUserRepository(params UserAccount[] users)
        {
            Users.AddRange(users);
        }

        private List<UserAccount> Users { get; } = [];

        public Task<IReadOnlyCollection<UserAccount>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<UserAccount>>(Users);

        public Task<UserAccount?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Users.FirstOrDefault(user => user.Id == id));

        public Task<UserAccount?> FindByUsernameAsync(string username, CancellationToken cancellationToken) =>
            Task.FromResult(Users.FirstOrDefault(user =>
                user.Username.Equals(username, StringComparison.OrdinalIgnoreCase)));

        public Task<UserAccount?> FindByUsernameOrEmailAsync(string usernameOrEmail, CancellationToken cancellationToken) =>
            Task.FromResult(Users.FirstOrDefault(user =>
                user.Username.Equals(usernameOrEmail, StringComparison.OrdinalIgnoreCase)
                || user.Email.Equals(usernameOrEmail, StringComparison.OrdinalIgnoreCase)));

        public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken) =>
            Task.FromResult(Users.Any(user => user.Username.Equals(username, StringComparison.OrdinalIgnoreCase)));

        public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult(Users.Any(user => user.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));

        public Task AddAsync(UserAccount user, CancellationToken cancellationToken)
        {
            Users.Add(user);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
