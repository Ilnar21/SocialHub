using SocialHub.Auth.Application.Abstractions;
using SocialHub.Auth.Application.Common;
using SocialHub.Auth.Application.Dtos;
using SocialHub.Auth.Application.Mapping;
using SocialHub.Auth.Domain.Entities;
using SocialHub.Auth.Domain.Enums;
using System.Security.Claims;

namespace SocialHub.Auth.Application.Services;

public sealed class AuthUserService(
    IUserRepository users,
    IAuthSessionRepository sessions,
    ILoginAuditRepository loginAudit,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    ITokenService tokenService)
{
    public async Task<ServiceResult<UserResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var validationError = ValidateRegistration(request);
        if (validationError is not null)
        {
            return ServiceResult<UserResponse>.Failure("validation_error", validationError);
        }

        var username = request.Username.Trim();
        var email = request.Email.Trim();

        if (await users.UsernameExistsAsync(username, cancellationToken))
        {
            return ServiceResult<UserResponse>.Failure("duplicate_username", "Username is already taken.");
        }

        if (await users.EmailExistsAsync(email, cancellationToken))
        {
            return ServiceResult<UserResponse>.Failure("duplicate_email", "Email is already taken.");
        }

        var user = new UserAccount
        {
            Username = username,
            Email = email,
            PasswordHash = passwordHasher.Hash(request.Password),
            Profile =
            {
                DisplayName = request.DisplayName.Trim(),
                Bio = NormalizeOptional(request.Bio)
            }
        };

        await users.AddAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<UserResponse>.Success(user.ToResponse());
    }

    public async Task<ServiceResult<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var usernameOrEmail = request.UsernameOrEmail.Trim();
        var user = await users.FindByUsernameOrEmailAsync(usernameOrEmail, cancellationToken);

        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            await AddLoginAuditAsync(usernameOrEmail, user?.Id, false, "Invalid credentials", cancellationToken);
            return ServiceResult<AuthResponse>.Failure("invalid_credentials", "Invalid username or password.");
        }

        if (user.Status == UserStatus.Blocked && (user.BlockedUntil is null || user.BlockedUntil > DateTimeOffset.UtcNow))
        {
            var blockedMessage = BuildBlockedLoginMessage(user);
            await AddLoginAuditAsync(usernameOrEmail, user.Id, false, blockedMessage, cancellationToken);
            return ServiceResult<AuthResponse>.Failure("account_blocked", blockedMessage);
        }

        if (user.Status == UserStatus.Blocked)
        {
            user.Status = UserStatus.Active;
            user.BlockReason = null;
            user.BlockedUntil = null;
            user.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var session = tokenService.CreateSession(user);
        await sessions.AddAsync(session, cancellationToken);
        await AddLoginAuditAsync(usernameOrEmail, user.Id, true, "Login succeeded", cancellationToken, saveImmediately: false);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<AuthResponse>.Success(new AuthResponse(session.Token, session.ExpiresAt, user.ToResponse()));
    }

    public async Task<ServiceResult<UserResponse>> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return ServiceResult<UserResponse>.Failure("user_not_found", "User was not found.");
        }

        if (!string.IsNullOrWhiteSpace(request.DisplayName))
        {
            user.Profile.DisplayName = request.DisplayName.Trim();
        }

        if (request.Bio is not null)
        {
            user.Profile.Bio = NormalizeOptional(request.Bio);
        }

        if (request.AvatarUrl is not null)
        {
            user.Profile.AvatarUrl = NormalizeOptional(request.AvatarUrl);
        }

        user.UpdatedAt = DateTimeOffset.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<UserResponse>.Success(user.ToResponse());
    }

    public async Task<ServiceResult<UserResponse>> BlockUserAsync(Guid moderatorId, Guid userId, BlockUserRequest request, CancellationToken cancellationToken)
    {
        var moderator = await users.FindByIdAsync(moderatorId, cancellationToken);
        if (moderator?.Role != UserRole.PlatformModerator)
        {
            return ServiceResult<UserResponse>.Failure("forbidden", "Only a platform moderator can block users.");
        }

        var user = await users.FindByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return ServiceResult<UserResponse>.Failure("user_not_found", "User was not found.");
        }

        if (moderatorId == userId)
        {
            return ServiceResult<UserResponse>.Failure("validation_error", "Moderator cannot block own account.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ServiceResult<UserResponse>.Failure("validation_error", "Block reason is required.");
        }

        user.Status = UserStatus.Blocked;
        user.BlockReason = request.Reason.Trim();
        user.BlockedUntil = request.BlockedUntil;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<UserResponse>.Success(user.ToResponse());
    }

    public async Task<ServiceResult<UserResponse>> SetStatusAsync(Guid userId, SetUserStatusRequest request, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return ServiceResult<UserResponse>.Failure("user_not_found", "User was not found.");
        }

        var status = request.Status.Trim().ToUpperInvariant();
        switch (status)
        {
            case "ACTIVE":
                user.Status = UserStatus.Active;
                user.BlockReason = null;
                user.BlockedUntil = null;
                break;

            case "BLOCKED":
                user.Status = UserStatus.Blocked;
                user.BlockReason = NormalizeOptional(request.Reason) ?? "Blocked by moderation.";
                user.BlockedUntil = request.ExpiresAtUtc;
                break;

            default:
                return ServiceResult<UserResponse>.Failure("validation_error", "Unknown user status.");
        }

        user.UpdatedAt = DateTimeOffset.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<UserResponse>.Success(user.ToResponse());
    }

    public async Task<ServiceResult<UserResponse>> ResolvePrincipalAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var account = await ResolveAccountAsync(principal, cancellationToken);
        return account is null
            ? ServiceResult<UserResponse>.Failure("unauthorized", "Authenticated user was not found or is blocked.")
            : ServiceResult<UserResponse>.Success(account.ToResponse());
    }

    public async Task<UserAccount?> ResolveAccountAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var idClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(idClaim, out var userId))
        {
            return null;
        }

        var user = await users.FindByIdAsync(userId, cancellationToken);
        return user?.Status == UserStatus.Active ? user : null;
    }

    private async Task AddLoginAuditAsync(
        string usernameOrEmail,
        Guid? userId,
        bool succeeded,
        string reason,
        CancellationToken cancellationToken,
        bool saveImmediately = true)
    {
        await loginAudit.AddAsync(new LoginAuditEntry
        {
            UsernameOrEmail = usernameOrEmail,
            UserId = userId,
            Succeeded = succeeded,
            Reason = reason
        }, cancellationToken);

        if (saveImmediately)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private static string? ValidateRegistration(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Trim().Length < 3)
        {
            return "Username must contain at least 3 characters.";
        }

        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
        {
            return "Valid email is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            return "Password must contain at least 8 characters.";
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return "Display name is required.";
        }

        return null;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string BuildBlockedLoginMessage(UserAccount user)
    {
        var reason = string.IsNullOrWhiteSpace(user.BlockReason)
            ? "нарушение правил платформы"
            : user.BlockReason.Trim();

        if (user.BlockedUntil is null)
        {
            return $"Вы заблокированы бессрочно. Причина: {reason}.";
        }

        var remaining = user.BlockedUntil.Value - DateTimeOffset.UtcNow;
        var days = Math.Max(1, (int)Math.Ceiling(remaining.TotalDays));
        return $"Вы заблокированы еще на {days} дн. Причина: {reason}.";
    }
}
