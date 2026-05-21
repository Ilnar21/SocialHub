namespace SocialHub.Moderation.Application.Models.Reports;

public sealed record CreateReportRequest(string TargetType, string TargetId, string Reason, string? Comment);
