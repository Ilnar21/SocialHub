namespace SocialHub.Community.Domain.Entities;

public sealed class CommunityAuditLog
{
    private CommunityAuditLog()
    {
    }

    public CommunityAuditLog(Guid communityId, Guid actorUserId, string action, string details, DateTime createdAtUtc)
    {
        Id = Guid.NewGuid();
        CommunityId = communityId;
        ActorUserId = actorUserId;
        Action = action;
        Details = details;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid CommunityId { get; private set; }
    public Guid ActorUserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string Details { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
}
