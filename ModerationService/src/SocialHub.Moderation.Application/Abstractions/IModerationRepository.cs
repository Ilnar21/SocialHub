using SocialHub.Moderation.Domain.Entities;
using SocialHub.Moderation.Domain.Enums;

namespace SocialHub.Moderation.Application.Abstractions;

public interface IModerationRepository
{
    Task AddReportAsync(ModerationReport report, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ModerationReport>> GetReportsAsync(ModerationReportStatus? status, CancellationToken cancellationToken);
    Task<ModerationReport?> GetReportAsync(Guid reportId, CancellationToken cancellationToken);
    Task<int> DeleteReportsByTargetAsync(string targetType, string targetId, CancellationToken cancellationToken);
    Task ResolveReportWithAuditAsync(ModerationReport report, AuditLog auditLog, CancellationToken cancellationToken);
    Task AddUserBlockWithAuditAsync(UserBlock block, AuditLog auditLog, CancellationToken cancellationToken);
    Task<AuditLog> AddAuditAsync(AuditLog auditLog, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AuditLog>> GetAuditAsync(string? actorUserId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken);
    Task AddSideEffectFailuresAsync(IReadOnlyCollection<SideEffectFailure> failures, CancellationToken cancellationToken);
}
