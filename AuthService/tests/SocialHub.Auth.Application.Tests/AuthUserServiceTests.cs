using System.Security.Claims;
using SocialHub.Auth.Application.Abstractions;
using SocialHub.Auth.Application.Dtos;
using SocialHub.Auth.Application.Services;
using SocialHub.Auth.Domain.Entities;
using SocialHub.Auth.Domain.Enums;

namespace SocialHub.Auth.Application.Tests;

public sealed class AuthUserServiceTests
{
    [Fact]
    public async Task Register_trims_profile_hashes_password_and_saves_user()
    {
        var users = new FakeUserRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(users, unitOfWork: unitOfWork);

        var result = await service.RegisterAsync(
            new RegisterRequest("  ivan  ", "  ivan@example.com  ", "Password123!", "  Ivan Petrov  ", "  bio  "),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("ivan", users.Users.Single().Username);
        Assert.Equal("ivan@example.com", users.Users.Single().Email);
        Assert.Equal("hashed:Password123!", users.Users.Single().PasswordHash);
        Assert.Equal("Ivan Petrov", result.Value!.Profile.DisplayName);
        Assert.Equal("bio", result.Value.Profile.Bio);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Theory]
    [InlineData("iv", "ivan@example.com", "Password123!", "Ivan", "Username must contain at least 3 characters.")]
    [InlineData("ivan", "invalid-email", "Password123!", "Ivan", "Valid email is required.")]
    [InlineData("ivan", "ivan@example.com", "short", "Ivan", "Password must contain at least 8 characters.")]
    [InlineData("ivan", "ivan@example.com", "Password123!", " ", "Display name is required.")]
    public async Task Register_rejects_invalid_input(
        string username,
        string email,
        string password,
        string displayName,
        string expectedMessage)
    {
        var users = new FakeUserRepository();
        var result = await CreateService(users).RegisterAsync(
            new RegisterRequest(username, email, password, displayName, null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("validation_error", result.ErrorCode);
        Assert.Equal(expectedMessage, result.ErrorMessage);
        Assert.Empty(users.Users);
    }

    [Fact]
    public async Task Register_rejects_duplicate_username_and_email_case_insensitively()
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

        var duplicateUsername = await service.RegisterAsync(
            new RegisterRequest("IVAN", "other@example.com", "Password123!", "Ivan 2", null),
            CancellationToken.None);
        var duplicateEmail = await service.RegisterAsync(
            new RegisterRequest("other", "IVAN@example.com", "Password123!", "Other", null),
            CancellationToken.None);

        Assert.Equal("duplicate_username", duplicateUsername.ErrorCode);
        Assert.Equal("duplicate_email", duplicateEmail.ErrorCode);
        Assert.Single(users.Users);
    }

    [Fact]
    public async Task Login_creates_session_and_success_audit()
    {
        var user = NewUser(passwordHash: "hashed:Password123!");
        var users = new FakeUserRepository(user);
        var sessions = new FakeAuthSessionRepository();
        var audit = new FakeLoginAuditRepository();

        var result = await CreateService(users, sessions, audit).LoginAsync(
            new LoginRequest("IVAN@example.com", "Password123!"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(user.Id, sessions.Sessions.Single().UserId);
        Assert.Equal("token:" + user.Id, result.Value!.Token);
        Assert.True(audit.Entries.Single().Succeeded);
    }

    [Fact]
    public async Task Login_rejects_bad_password_and_writes_audit()
    {
        var user = NewUser(passwordHash: "hashed:Password123!");
        var audit = new FakeLoginAuditRepository();

        var result = await CreateService(new FakeUserRepository(user), audit: audit).LoginAsync(
            new LoginRequest("ivan", "WrongPassword"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_credentials", result.ErrorCode);
        Assert.False(audit.Entries.Single().Succeeded);
        Assert.Equal(user.Id, audit.Entries.Single().UserId);
    }

    [Fact]
    public async Task Login_rejects_currently_blocked_user()
    {
        var blocked = NewUser(passwordHash: "hashed:Password123!");
        blocked.Status = UserStatus.Blocked;
        blocked.BlockReason = "policy";
        blocked.BlockedUntil = DateTimeOffset.UtcNow.AddHours(1);
        var audit = new FakeLoginAuditRepository();

        var result = await CreateService(new FakeUserRepository(blocked), audit: audit).LoginAsync(
            new LoginRequest("ivan", "Password123!"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("account_blocked", result.ErrorCode);
        Assert.Contains("policy", result.ErrorMessage);
        Assert.False(audit.Entries.Single().Succeeded);
        Assert.Equal(result.ErrorMessage, audit.Entries.Single().Reason);
    }

    [Fact]
    public async Task Login_reactivates_expired_block_before_creating_session()
    {
        var user = NewUser(passwordHash: "hashed:Password123!");
        user.Status = UserStatus.Blocked;
        user.BlockReason = "expired";
        user.BlockedUntil = DateTimeOffset.UtcNow.AddMinutes(-1);

        var result = await CreateService(new FakeUserRepository(user)).LoginAsync(
            new LoginRequest("ivan", "Password123!"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Null(user.BlockReason);
        Assert.Null(user.BlockedUntil);
    }

    [Fact]
    public async Task UpdateProfile_updates_only_supplied_fields()
    {
        var user = NewUser();
        user.Profile.Bio = "old bio";
        user.Profile.AvatarUrl = "https://old.example/avatar.png";

        var result = await CreateService(new FakeUserRepository(user)).UpdateProfileAsync(
            user.Id,
            new UpdateProfileRequest("  New Name  ", "", null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("New Name", user.Profile.DisplayName);
        Assert.Null(user.Profile.Bio);
        Assert.Equal("https://old.example/avatar.png", user.Profile.AvatarUrl);
    }

    [Fact]
    public async Task BlockUser_requires_platform_moderator_and_reason()
    {
        var moderator = NewUser(username: "moderator", role: UserRole.User);
        var target = NewUser(username: "target", email: "target@example.com");
        var service = CreateService(new FakeUserRepository(moderator, target));

        var forbidden = await service.BlockUserAsync(
            moderator.Id,
            target.Id,
            new BlockUserRequest("spam", null),
            CancellationToken.None);

        moderator.Role = UserRole.PlatformModerator;
        var invalid = await service.BlockUserAsync(
            moderator.Id,
            target.Id,
            new BlockUserRequest(" ", null),
            CancellationToken.None);

        Assert.Equal("forbidden", forbidden.ErrorCode);
        Assert.Equal("validation_error", invalid.ErrorCode);
    }

    [Fact]
    public async Task SetStatus_blocks_and_reactivates_user_for_internal_side_effects()
    {
        var user = NewUser();
        var service = CreateService(new FakeUserRepository(user));

        var blockedUntil = DateTimeOffset.UtcNow.AddDays(2);
        var blocked = await service.SetStatusAsync(
            user.Id,
            new SetUserStatusRequest("BLOCKED", "rule violation", blockedUntil),
            CancellationToken.None);
        var active = await service.SetStatusAsync(
            user.Id,
            new SetUserStatusRequest("ACTIVE", null, null),
            CancellationToken.None);

        Assert.True(blocked.IsSuccess);
        Assert.True(active.IsSuccess);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Null(user.BlockReason);
        Assert.Null(user.BlockedUntil);
    }

    [Fact]
    public async Task ResolvePrincipal_returns_only_active_user()
    {
        var user = NewUser();
        var service = CreateService(new FakeUserRepository(user));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())],
            "Test"));

        var active = await service.ResolvePrincipalAsync(principal, CancellationToken.None);
        user.Status = UserStatus.Blocked;
        var blocked = await service.ResolvePrincipalAsync(principal, CancellationToken.None);

        Assert.True(active.IsSuccess);
        Assert.Equal("unauthorized", blocked.ErrorCode);
    }

    private static AuthUserService CreateService(
        FakeUserRepository? users = null,
        FakeAuthSessionRepository? sessions = null,
        FakeLoginAuditRepository? audit = null,
        FakeUnitOfWork? unitOfWork = null)
    {
        return new AuthUserService(
            users ?? new FakeUserRepository(),
            sessions ?? new FakeAuthSessionRepository(),
            audit ?? new FakeLoginAuditRepository(),
            unitOfWork ?? new FakeUnitOfWork(),
            new FakePasswordHasher(),
            new FakeTokenService());
    }

    private static UserAccount NewUser(
        string username = "ivan",
        string email = "ivan@example.com",
        string passwordHash = "hashed:Password123!",
        UserRole role = UserRole.User) =>
        new()
        {
            Username = username,
            Email = email,
            PasswordHash = passwordHash,
            Role = role,
            Profile = { DisplayName = username }
        };

    private sealed class FakeUserRepository : IUserRepository
    {
        public FakeUserRepository(params UserAccount[] users)
        {
            Users.AddRange(users);
        }

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
        public int SaveCount { get; private set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
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
