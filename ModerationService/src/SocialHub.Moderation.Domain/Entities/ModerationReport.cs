using SocialHub.Moderation.Domain.Enums;

namespace SocialHub.Moderation.Domain.Entities;

public sealed class ModerationReport
{
    public ModerationReport(
        Guid id,
        string reporterUserId,
        string targetType,
        string targetId,
        string reason,
        string? comment,
        ModerationReportStatus status,
        string? resolutionComment,
        string? resolvedByUserId,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        Id = id;
        ReporterUserId = reporterUserId;
        TargetType = targetType;
        TargetId = targetId;
        Reason = reason;
        Comment = comment;
        Status = status;
        ResolutionComment = resolutionComment;
        ResolvedByUserId = resolvedByUserId;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public Guid Id { get; }
    public string ReporterUserId { get; }
    public string TargetType { get; }
    public string TargetId { get; }
    public string Reason { get; }
    public string? Comment { get; }
    public ModerationReportStatus Status { get; }
    public string? ResolutionComment { get; }
    public string? ResolvedByUserId { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset UpdatedAtUtc { get; }

    public ModerationReport Resolve(string moderatorUserId, string resolutionComment, DateTimeOffset now)
    {
        return new ModerationReport(
            Id,
            ReporterUserId,
            TargetType,
            TargetId,
            Reason,
            Comment,
            ModerationReportStatus.Resolved,
            resolutionComment,
            moderatorUserId,
            CreatedAtUtc,
            now);
    }
}
