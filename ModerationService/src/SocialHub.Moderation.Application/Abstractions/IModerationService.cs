using SocialHub.Moderation.Application.Models.Audit;
using SocialHub.Moderation.Application.Models.Blocks;
using SocialHub.Moderation.Application.Models.Reports;

namespace SocialHub.Moderation.Application.Abstractions;

public interface IModerationService
{
    Task<ReportResponse> CreateReportAsync(CreateReportRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ReportResponse>> GetReportsAsync(string? status, CancellationToken cancellationToken);
    Task<ReportResponse> DeleteReportedPostAsync(Guid reportId, ResolveReportRequest request, CancellationToken cancellationToken);
    Task<BlockResponse> BlockUserAsync(string userId, BlockUserRequest request, CancellationToken cancellationToken);
    Task<AuditResponse> UnblockUserAsync(string userId, CancellationToken cancellationToken);
    Task<AuditResponse> CreateAuditAsync(CreateAuditRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AuditResponse>> GetAuditAsync(string? actorUserId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken);
}
