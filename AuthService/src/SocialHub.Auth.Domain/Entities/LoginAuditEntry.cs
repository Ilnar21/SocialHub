namespace SocialHub.Auth.Domain.Entities;

public sealed class LoginAuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UsernameOrEmail { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public UserAccount? User { get; set; }
    public bool Succeeded { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
