namespace SocialHub.Moderation.Application.Models.Audit;

public sealed record AuditResponse(
    Guid Id,
    string ActorUserId,
    string ActorRole,
    string Action,
    string TargetType,
    string TargetId,
    string? CommunityId,
    string Reason,
    DateTimeOffset CreatedAtUtc);
