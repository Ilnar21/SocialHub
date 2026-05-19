using AuthService.Domain.Enums;

namespace AuthService.Application.Dtos;

public sealed record RegisterRequest(
    string Username,
    string Email,
    string Password,
    string DisplayName,
    string? Bio);

public sealed record LoginRequest(string UsernameOrEmail, string Password);

public sealed record UpdateProfileRequest(string? DisplayName, string? Bio, string? AvatarUrl);

public sealed record BlockUserRequest(string Reason, DateTimeOffset? BlockedUntil);

public sealed record UserResponse(
    Guid Id,
    string Username,
    string Email,
    UserRole Role,
    UserStatus Status,
    string? BlockReason,
    DateTimeOffset? BlockedUntil,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    ProfileResponse Profile);

public sealed record ProfileResponse(string DisplayName, string? Bio, string? AvatarUrl);

public sealed record AuthResponse(string Token, DateTimeOffset ExpiresAt, UserResponse User);

public sealed record ErrorResponse(string Code, string Message);
