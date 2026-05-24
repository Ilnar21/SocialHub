using SocialHub.Moderation.Application.Abstractions;
using SocialHub.Moderation.Application.Exceptions;
using SocialHub.Moderation.Application.Models.Audit;
using SocialHub.Moderation.Application.Models.Blocks;
using SocialHub.Moderation.Application.Models.External;
using SocialHub.Moderation.Application.Models.Reports;
using SocialHub.Moderation.Domain.Entities;
using SocialHub.Moderation.Domain.Enums;

namespace SocialHub.Moderation.Application.Services;

public sealed class ModerationService : IModerationService
{
    private readonly IModerationRepository _repository;
    private readonly ICurrentUserContext _currentUser;
    private readonly IExternalModerationClient _externalClient;

    public ModerationService(
        IModerationRepository repository,
        ICurrentUserContext currentUser,
        IExternalModerationClient externalClient)
    {
        _repository = repository;
        _currentUser = currentUser;
        _externalClient = externalClient;
    }

    public async Task<ReportResponse> CreateReportAsync(CreateReportRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TargetType) || string.IsNullOrWhiteSpace(request.TargetId) || string.IsNullOrWhiteSpace(request.Reason))
        {
            throw AppException.BadRequest("Target type, target id and reason are required.");
        }

        var now = DateTimeOffset.UtcNow;
        var report = new ModerationReport(
            Guid.NewGuid(),
            _currentUser.UserId,
            request.TargetType.Trim().ToUpperInvariant(),
            request.TargetId.Trim(),
            request.Reason.Trim(),
            request.Comment?.Trim(),
            ModerationReportStatus.New,
            null,
            null,
            now,
            now);

        await _repository.AddReportAsync(report, cancellationToken);
        return ToReportResponse(report);
    }

    public async Task<IReadOnlyCollection<ReportResponse>> GetReportsAsync(string? status, CancellationToken cancellationToken)
    {
        var parsedStatus = ParseStatus(status);
        var reports = await _repository.GetReportsAsync(parsedStatus, cancellationToken);
        return reports.Select(ToReportResponse).ToArray();
    }

    public async Task<ReportResponse> DeleteReportedPostAsync(Guid reportId, ResolveReportRequest request, CancellationToken cancellationToken)
    {
        EnsurePlatformModerator();

        var report = await _repository.GetReportAsync(reportId, cancellationToken)
            ?? throw AppException.NotFound("Report was not found.");

        var reason = request.Comment?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            reason = report.Reason;
        }

        var now = DateTimeOffset.UtcNow;
        var resolved = report.Resolve(_currentUser.UserId, reason, now);
        var audit = CreateAudit("POST_DELETED", "POST", report.TargetId, reason, null, "PLATFORM_MODERATOR", now);

        await _repository.ResolveReportWithAuditAsync(resolved, audit, cancellationToken);
        var sideEffects = new[]
        {
            await _externalClient.DeletePostAsync(report.TargetId, reason, cancellationToken),
            await _externalClient.NotifyPostDeletedAsync(report.TargetId, reason, cancellationToken)
        };
        await SaveFailedSideEffectsAsync(sideEffects, "POST_DELETED", "POST", report.TargetId, cancellationToken);

        return ToReportResponse(resolved);
    }

    public async Task<BlockResponse> BlockUserAsync(string userId, BlockUserRequest request, CancellationToken cancellationToken)
    {
        EnsurePlatformModerator();

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(request.Reason) || request.DurationDays <= 0)
        {
            throw AppException.BadRequest("User id, reason and positive duration are required.");
        }

        if (userId.Trim().Equals(_currentUser.UserId, StringComparison.OrdinalIgnoreCase))
        {
            throw AppException.BadRequest("Moderator cannot block own account.");
        }

        var now = DateTimeOffset.UtcNow;
        var block = new UserBlock(
            Guid.NewGuid(),
            userId.Trim(),
            _currentUser.UserId,
            request.Reason.Trim(),
            now,
            now.AddDays(request.DurationDays));

        var audit = CreateAudit("USER_BLOCKED", "USER", block.BlockedUserId, block.Reason, null, "PLATFORM_MODERATOR", now);
        await _repository.AddUserBlockWithAuditAsync(block, audit, cancellationToken);
        var sideEffects = new[]
        {
            await _externalClient.SetUserBlockedAsync(block, cancellationToken),
            await _externalClient.NotifyUserBlockedAsync(block, cancellationToken)
        };
        await SaveFailedSideEffectsAsync(sideEffects, "USER_BLOCKED", "USER", block.BlockedUserId, cancellationToken);

        return ToBlockResponse(block);
    }

    public async Task<AuditResponse> UnblockUserAsync(string userId, CancellationToken cancellationToken)
    {
        EnsurePlatformModerator();

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw AppException.BadRequest("User id is required.");
        }

        if (userId.Trim().Equals(_currentUser.UserId, StringComparison.OrdinalIgnoreCase))
        {
            throw AppException.BadRequest("Moderator cannot change own block status.");
        }

        var now = DateTimeOffset.UtcNow;
        var audit = CreateAudit("USER_UNBLOCKED", "USER", userId, "User unblocked by platform moderator.", null, "PLATFORM_MODERATOR", now);
        var savedAudit = await _repository.AddAuditAsync(audit, cancellationToken);
        var sideEffect = await _externalClient.SetUserActiveAsync(userId.Trim(), cancellationToken);
        await SaveFailedSideEffectsAsync([sideEffect], "USER_UNBLOCKED", "USER", userId.Trim(), cancellationToken);

        return ToAuditResponse(savedAudit);
    }

    public async Task<AuditResponse> CreateAuditAsync(CreateAuditRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Action) || string.IsNullOrWhiteSpace(request.TargetType) || string.IsNullOrWhiteSpace(request.TargetId))
        {
            throw AppException.BadRequest("Action, target type and target id are required.");
        }

        var audit = CreateAudit(
            request.Action,
            request.TargetType,
            request.TargetId,
            request.Reason ?? "",
            request.CommunityId,
            request.ActorRole ?? "UNKNOWN",
            DateTimeOffset.UtcNow);

        return ToAuditResponse(await _repository.AddAuditAsync(audit, cancellationToken));
    }

    public async Task<IReadOnlyCollection<AuditResponse>> GetAuditAsync(string? actorUserId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken)
    {
        var entries = await _repository.GetAuditAsync(actorUserId, from, to, cancellationToken);
        return entries.Select(ToAuditResponse).ToArray();
    }

    private void EnsurePlatformModerator()
    {
        var role = _currentUser.PlatformRole;
        if (role is null || (!role.Equals("PLATFORM_MODERATOR", StringComparison.OrdinalIgnoreCase)
            && !role.Equals("PLATFORMMODERATOR", StringComparison.OrdinalIgnoreCase)
            && !role.Equals("PlatformModerator", StringComparison.OrdinalIgnoreCase)
            && !role.Equals("MODERATOR", StringComparison.OrdinalIgnoreCase)))
        {
            throw AppException.Forbidden("Platform moderator role is required.");
        }
    }

    private async Task SaveFailedSideEffectsAsync(
        IReadOnlyCollection<SideEffectResult> results,
        string action,
        string targetType,
        string targetId,
        CancellationToken cancellationToken)
    {
        var failures = results
            .Where(x => !x.Succeeded)
            .Select(x => new SideEffectFailure(
                Guid.NewGuid(),
                action,
                targetType,
                targetId,
                x.ServiceName,
                x.RequestPath,
                x.ErrorMessage ?? "Unknown external service error.",
                DateTimeOffset.UtcNow))
            .ToArray();

        if (failures.Length > 0)
        {
            await _repository.AddSideEffectFailuresAsync(failures, cancellationToken);
        }
    }

    private AuditLog CreateAudit(string action, string targetType, string targetId, string reason, string? communityId, string actorRole, DateTimeOffset now)
    {
        return new AuditLog(
            Guid.NewGuid(),
            _currentUser.UserId,
            actorRole.Trim().ToUpperInvariant(),
            action.Trim().ToUpperInvariant(),
            targetType.Trim().ToUpperInvariant(),
            targetId.Trim(),
            communityId?.Trim(),
            reason.Trim(),
            now);
    }

    private static ModerationReportStatus? ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        return status.Trim().ToUpperInvariant() switch
        {
            "NEW" => ModerationReportStatus.New,
            "RESOLVED" => ModerationReportStatus.Resolved,
            _ => throw AppException.BadRequest("Unknown report status.")
        };
    }

    private static ReportResponse ToReportResponse(ModerationReport report)
    {
        return new ReportResponse(
            report.Id,
            report.ReporterUserId,
            report.TargetType,
            report.TargetId,
            report.Reason,
            report.Comment,
            ToDatabaseStatus(report.Status),
            report.ResolutionComment,
            report.ResolvedByUserId,
            report.CreatedAtUtc,
            report.UpdatedAtUtc);
    }

    private static BlockResponse ToBlockResponse(UserBlock block)
    {
        return new BlockResponse(block.Id, block.BlockedUserId, block.ModeratorUserId, block.Reason, block.BlockedAtUtc, block.ExpiresAtUtc);
    }

    private static AuditResponse ToAuditResponse(AuditLog audit)
    {
        return new AuditResponse(audit.Id, audit.ActorUserId, audit.ActorRole, audit.Action, audit.TargetType, audit.TargetId, audit.CommunityId, audit.Reason, audit.CreatedAtUtc);
    }

    private static string ToDatabaseStatus(ModerationReportStatus status) => status switch
    {
        ModerationReportStatus.New => "NEW",
        ModerationReportStatus.Resolved => "RESOLVED",
        _ => status.ToString().ToUpperInvariant()
    };
}
