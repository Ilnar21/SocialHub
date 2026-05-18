namespace SocialHub.Moderation.Domain.Entities;

public sealed class UserBlock
{
    public UserBlock(
        Guid id,
        string blockedUserId,
        string moderatorUserId,
        string reason,
        DateTimeOffset blockedAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        Id = id;
        BlockedUserId = blockedUserId;
        ModeratorUserId = moderatorUserId;
        Reason = reason;
        BlockedAtUtc = blockedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid Id { get; }
    public string BlockedUserId { get; }
    public string ModeratorUserId { get; }
    public string Reason { get; }
    public DateTimeOffset BlockedAtUtc { get; }
    public DateTimeOffset ExpiresAtUtc { get; }
}
