namespace SocialHub.Auth.Domain.Entities;

public sealed class AuthSession
{
    public string Token { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public UserAccount? User { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
