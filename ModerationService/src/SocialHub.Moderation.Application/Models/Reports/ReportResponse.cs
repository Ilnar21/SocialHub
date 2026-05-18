namespace SocialHub.Moderation.Application.Models.Reports;

public sealed record ReportResponse(
    Guid Id,
    string ReporterUserId,
    string TargetType,
    string TargetId,
    string Reason,
    string? Comment,
    string Status,
    string? ResolutionComment,
    string? ResolvedByUserId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
