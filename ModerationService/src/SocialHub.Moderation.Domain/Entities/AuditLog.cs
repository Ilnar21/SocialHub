namespace SocialHub.Moderation.Domain.Entities;

public sealed class AuditLog
{
    public AuditLog(
        Guid id,
        string actorUserId,
        string actorRole,
        string action,
        string targetType,
        string targetId,
        string? communityId,
        string reason,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        ActorUserId = actorUserId;
        ActorRole = actorRole;
        Action = action;
        TargetType = targetType;
        TargetId = targetId;
        CommunityId = communityId;
        Reason = reason;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; }
    public string ActorUserId { get; }
    public string ActorRole { get; }
    public string Action { get; }
    public string TargetType { get; }
    public string TargetId { get; }
    public string? CommunityId { get; }
    public string Reason { get; }
    public DateTimeOffset CreatedAtUtc { get; }
}
