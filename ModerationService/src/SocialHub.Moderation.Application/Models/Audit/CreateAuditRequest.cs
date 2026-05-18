namespace SocialHub.Moderation.Application.Models.Audit;

public sealed record CreateAuditRequest(string Action, string TargetType, string TargetId, string? Reason, string? CommunityId, string? ActorRole);
