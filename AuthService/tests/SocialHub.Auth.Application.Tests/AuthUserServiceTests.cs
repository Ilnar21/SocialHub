using SocialHub.Auth.Application.Abstractions;
using SocialHub.Auth.Application.Dtos;
using SocialHub.Auth.Application.Services;
using SocialHub.Auth.Domain.Entities;
using SocialHub.Auth.Domain.Enums;

namespace SocialHub.Auth.Application.Tests;

public sealed class AuthUserServiceTests
{
    [Fact]
    public async Task Register_rejects_duplicate_username()
    {
        var users = new FakeUserRepository();
        users.Users.Add(new UserAccount
        {
            Username = "ivan",
            Email = "ivan@example.com",
            PasswordHash = "hashed:Password123!",
            Profile = { DisplayName = "Ivan" }
        });

        var service = CreateService(users);

        var result = await service.RegisterAsync(
            new RegisterRequest("ivan", "ivan2@example.com", "Password123!", "Ivan 2", null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("duplicate_username", result.ErrorCode);
        Assert.Single(users.Users);
    }

    [Fact]
    public async Task Login_rejects_blocked_user_and_writes_audit()
    {
        var user = new UserAccount
        {
            Username = "blocked",
            Email = "blocked@example.com",
            PasswordHash = "hashed:Password123!",
            Status = UserStatus.Blocked,
            BlockedUntil = DateTimeOffset.UtcNow.AddDays(1),
            Profile = { DisplayName = "Blocked User" }
        };
        var users = new FakeUserRepository();
        users.Users.Add(user);
        var audit = new FakeLoginAuditRepository();
        var service = CreateService(users, audit: audit);

        var result = await service.LoginAsync(
            new LoginRequest("blocked", "Password123!"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("account_blocked", result.ErrorCode);
        Assert.Single(audit.Entries);
        Assert.False(audit.Entries[0].Succeeded);
        Assert.Equal("Account is blocked", audit.Entries[0].Reason);
    }

    [Fact]
    public async Task Platform_moderator_can_block_user()
    {
        var moderator = new UserAccount
        {
            Username = "moderator",
            Email = "moderator@example.com",
            Role = UserRole.PlatformModerator,
            Profile = { DisplayName = "Moderator" }
        };
        var target = new UserAccount
        {
            Username = "target",
            Email = "target@example.com",
            Profile = { DisplayName = "Target" }
        };
        var users = new FakeUserRepository();
        users.Users.AddRange([moderator, target]);
        var service = CreateService(users);

        var result = await service.BlockUserAsync(
            moderator.Id,
            target.Id,
            new BlockUserRequest("spam", DateTimeOffset.UtcNow.AddDays(3)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Blocked, target.Status);
        Assert.Equal("spam", target.BlockReason);
    }

    private static AuthUserService CreateService(
        FakeUserRepository? users = null,
        FakeLoginAuditRepository? audit = null)
    {
        return new AuthUserService(
            users ?? new FakeUserRepository(),
            new FakeAuthSessionRepository(),
            audit ?? new FakeLoginAuditRepository(),
            new FakeUnitOfWork(),
            new FakePasswordHasher(),
            new FakeTokenService());
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public List<UserAccount> Users { get; } = [];

        public Task<IReadOnlyCollection<UserAccount>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<UserAccount>>(Users);

        public Task<UserAccount?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Users.FirstOrDefault(user => user.Id == id));

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

    private sealed class FakeAuthSessionRepository : IAuthSessionRepository
    {
        public List<AuthSession> Sessions { get; } = [];

        public Task AddAsync(AuthSession session, CancellationToken cancellationToken)
        {
            Sessions.Add(session);
            return Task.CompletedTask;
        }

        public Task<UserAccount?> FindUserByTokenAsync(string token, CancellationToken cancellationToken) =>
            Task.FromResult<UserAccount?>(null);
    }

    private sealed class FakeLoginAuditRepository : ILoginAuditRepository
    {
        public List<LoginAuditEntry> Entries { get; } = [];

        public Task AddAsync(LoginAuditEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<LoginAuditEntry>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<LoginAuditEntry>>(Entries);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hashed:{password}";

        public bool Verify(string password, string passwordHash) => passwordHash == Hash(password);
    }

    private sealed class FakeTokenService : ITokenService
    {
        public AuthSession CreateSession(UserAccount user) => new()
        {
            Token = $"token:{user.Id}",
            UserId = user.Id,
            User = user,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };
    }
}
