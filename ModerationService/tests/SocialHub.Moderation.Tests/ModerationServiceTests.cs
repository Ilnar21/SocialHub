using NUnit.Framework;
using SocialHub.Moderation.Application.Abstractions;
using SocialHub.Moderation.Application.Exceptions;
using SocialHub.Moderation.Application.Models.Audit;
using SocialHub.Moderation.Application.Models.Blocks;
using SocialHub.Moderation.Application.Models.External;
using SocialHub.Moderation.Application.Models.Reports;
using SocialHub.Moderation.Domain.Entities;
using SocialHub.Moderation.Domain.Enums;
using ModerationAppService = SocialHub.Moderation.Application.Services.ModerationService;

namespace SocialHub.Moderation.Tests;

[TestFixture]
public sealed class ModerationServiceTests
{
    [Test]
    public async Task CreateReportAsync_creates_new_report()
    {
        var repository = new InMemoryModerationRepository();
        var service = CreateService(repository, "ivan.petrov", null);

        var response = await service.CreateReportAsync(new CreateReportRequest("POST", "post-1", "Спам", "Реклама"), CancellationToken.None);

        Assert.That(response.Status, Is.EqualTo("NEW"));
        Assert.That(repository.Reports, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task DeleteReportedPostAsync_resolves_report_and_writes_audit()
    {
        var repository = new InMemoryModerationRepository();
        var report = NewReport("post-1");
        repository.Reports[report.Id] = report;
        var service = CreateService(repository, "pavel.mod", "PLATFORM_MODERATOR");

        var response = await service.DeleteReportedPostAsync(report.Id, new ResolveReportRequest("Удалено"), CancellationToken.None);

        Assert.That(response.Status, Is.EqualTo("RESOLVED"));
        Assert.That(repository.AuditLogs.Single().Action, Is.EqualTo("POST_DELETED"));
    }

    [Test]
    public void DeleteReportedPostAsync_rejects_non_post_report()
    {
        var repository = new InMemoryModerationRepository();
        var report = NewReport("community-1", "COMMUNITY");
        repository.Reports[report.Id] = report;
        var service = CreateService(repository, "pavel.mod", "PLATFORM_MODERATOR");

        var ex = Assert.ThrowsAsync<AppException>(() => service.DeleteReportedPostAsync(report.Id, new ResolveReportRequest("reviewed"), CancellationToken.None));

        Assert.That(ex!.StatusCode, Is.EqualTo(400));
    }

    [Test]
    public async Task ResolveReportAsync_resolves_community_report_and_writes_audit()
    {
        var repository = new InMemoryModerationRepository();
        var report = NewReport("community-1", "COMMUNITY");
        repository.Reports[report.Id] = report;
        var service = CreateService(repository, "pavel.mod", "PLATFORM_MODERATOR");

        var response = await service.ResolveReportAsync(report.Id, new ResolveReportRequest("reviewed"), CancellationToken.None);

        Assert.That(response.Status, Is.EqualTo("RESOLVED"));
        Assert.That(repository.AuditLogs.Single().Action, Is.EqualTo("COMMUNITY_REPORT_RESOLVED"));
        Assert.That(repository.AuditLogs.Single().CommunityId, Is.EqualTo("community-1"));
    }

    [Test]
    public async Task BlockUserAsync_creates_block_and_audit()
    {
        var repository = new InMemoryModerationRepository();
        var service = CreateService(repository, "pavel.mod", "PLATFORM_MODERATOR");

        var response = await service.BlockUserAsync("ivan.petrov", new BlockUserRequest(7, "Повторный спам"), CancellationToken.None);

        Assert.That(response.BlockedUserId, Is.EqualTo("ivan.petrov"));
        Assert.That(repository.Blocks, Has.Count.EqualTo(1));
        Assert.That(repository.AuditLogs.Single().Action, Is.EqualTo("USER_BLOCKED"));
    }

    [Test]
    public void BlockUserAsync_requires_platform_moderator()
    {
        var service = CreateService(new InMemoryModerationRepository(), "ivan.petrov", "USER");

        var ex = Assert.ThrowsAsync<AppException>(() => service.BlockUserAsync("maria.sokolova", new BlockUserRequest(7, "reason"), CancellationToken.None));
        Assert.That(ex!.StatusCode, Is.EqualTo(403));
    }

    [Test]
    public void BlockUserAsync_rejects_platform_moderator_target()
    {
        var external = new FakeExternalClient(targetRole: "PlatformModerator");
        var service = CreateService(new InMemoryModerationRepository(), "pavel.mod", "PLATFORM_MODERATOR", external);

        var ex = Assert.ThrowsAsync<AppException>(() => service.BlockUserAsync("moderator-id", new BlockUserRequest(7, "reason"), CancellationToken.None));

        Assert.That(ex!.StatusCode, Is.EqualTo(400));
    }

    [Test]
    public async Task UnblockUserAsync_writes_audit_and_sets_user_active()
    {
        var repository = new InMemoryModerationRepository();
        var external = new FakeExternalClient();
        var service = CreateService(repository, "pavel.mod", "PLATFORM_MODERATOR", external);

        var response = await service.UnblockUserAsync("ivan.petrov", CancellationToken.None);

        Assert.That(response.Action, Is.EqualTo("USER_UNBLOCKED"));
        Assert.That(repository.AuditLogs.Single().TargetId, Is.EqualTo("ivan.petrov"));
        Assert.That(external.SetActiveCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task BlockCommunityAsync_sets_status_and_writes_audit()
    {
        var repository = new InMemoryModerationRepository();
        var external = new FakeExternalClient();
        var service = CreateService(repository, "pavel.mod", "PLATFORM_MODERATOR", external);

        var response = await service.BlockCommunityAsync("community-1", new BlockCommunityRequest("spam"), CancellationToken.None);

        Assert.That(response.Action, Is.EqualTo("COMMUNITY_BLOCKED"));
        Assert.That(response.TargetType, Is.EqualTo("COMMUNITY"));
        Assert.That(repository.AuditLogs.Single().CommunityId, Is.EqualTo("community-1"));
        Assert.That(external.SetCommunityBlockedCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task UnblockCommunityAsync_sets_status_and_writes_audit()
    {
        var repository = new InMemoryModerationRepository();
        var external = new FakeExternalClient();
        var service = CreateService(repository, "pavel.mod", "PLATFORM_MODERATOR", external);

        var response = await service.UnblockCommunityAsync("community-1", CancellationToken.None);

        Assert.That(response.Action, Is.EqualTo("COMMUNITY_UNBLOCKED"));
        Assert.That(repository.AuditLogs.Single().TargetId, Is.EqualTo("community-1"));
        Assert.That(external.SetCommunityActiveCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task External_service_failures_are_saved_without_breaking_action()
    {
        var repository = new InMemoryModerationRepository();
        var report = NewReport("post-1");
        repository.Reports[report.Id] = report;
        var external = new FakeExternalClient(fail: true);
        var service = CreateService(repository, "pavel.mod", "PLATFORM_MODERATOR", external);

        var response = await service.DeleteReportedPostAsync(report.Id, new ResolveReportRequest("Удалено"), CancellationToken.None);

        Assert.That(response.Status, Is.EqualTo("RESOLVED"));
        Assert.That(repository.SideEffectFailures, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task CreateAuditAsync_writes_audit_entry()
    {
        var repository = new InMemoryModerationRepository();
        var service = CreateService(repository, "anna.admin", "COMMUNITY_ADMIN");

        var response = await service.CreateAuditAsync(new CreateAuditRequest("LOCAL_POST_DELETED", "POST", "post-1", "local", "community-1", "COMMUNITY_ADMIN"), CancellationToken.None);

        Assert.That(response.Action, Is.EqualTo("LOCAL_POST_DELETED"));
        Assert.That(repository.AuditLogs, Has.Count.EqualTo(1));
    }

    private static ModerationAppService CreateService(
        InMemoryModerationRepository repository,
        string userId,
        string? role,
        IExternalModerationClient? external = null)
    {
        return new ModerationAppService(repository, new TestCurrentUserContext(userId, role), external ?? new FakeExternalClient());
    }

    private static ModerationReport NewReport(string targetId, string targetType = "POST")
    {
        var now = DateTimeOffset.UtcNow;
        return new ModerationReport(Guid.NewGuid(), "ivan.petrov", targetType, targetId, "Спам", null, ModerationReportStatus.New, null, null, now, now);
    }

    private sealed class InMemoryModerationRepository : IModerationRepository
    {
        public Dictionary<Guid, ModerationReport> Reports { get; } = new();
        public List<UserBlock> Blocks { get; } = new();
        public List<AuditLog> AuditLogs { get; } = new();
        public List<SideEffectFailure> SideEffectFailures { get; } = new();

        public Task AddReportAsync(ModerationReport report, CancellationToken cancellationToken)
        {
            Reports[report.Id] = report;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<ModerationReport>> GetReportsAsync(ModerationReportStatus? status, CancellationToken cancellationToken)
        {
            var reports = Reports.Values.Where(x => status is null || x.Status == status).ToArray();
            return Task.FromResult<IReadOnlyCollection<ModerationReport>>(reports);
        }

        public Task<ModerationReport?> GetReportAsync(Guid reportId, CancellationToken cancellationToken)
        {
            Reports.TryGetValue(reportId, out var report);
            return Task.FromResult(report);
        }

        public Task ResolveReportWithAuditAsync(ModerationReport report, AuditLog auditLog, CancellationToken cancellationToken)
        {
            Reports[report.Id] = report;
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task AddUserBlockWithAuditAsync(UserBlock block, AuditLog auditLog, CancellationToken cancellationToken)
        {
            Blocks.Add(block);
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task<AuditLog> AddAuditAsync(AuditLog auditLog, CancellationToken cancellationToken)
        {
            AuditLogs.Add(auditLog);
            return Task.FromResult(auditLog);
        }

        public Task<IReadOnlyCollection<AuditLog>> GetAuditAsync(string? actorUserId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<AuditLog>>(AuditLogs.ToArray());
        }

        public Task AddSideEffectFailuresAsync(IReadOnlyCollection<SideEffectFailure> failures, CancellationToken cancellationToken)
        {
            SideEffectFailures.AddRange(failures);
            return Task.CompletedTask;
        }
    }

    private sealed class TestCurrentUserContext : ICurrentUserContext
    {
        public TestCurrentUserContext(string userId, string? platformRole)
        {
            UserId = userId;
            PlatformRole = platformRole;
        }

        public string UserId { get; }
        public string? PlatformRole { get; }
    }

    private sealed class FakeExternalClient : IExternalModerationClient
    {
        private readonly bool _fail;
        private readonly string _targetRole;

        public FakeExternalClient(bool fail = false, string targetRole = "User")
        {
            _fail = fail;
            _targetRole = targetRole;
        }

        public int SetActiveCalls { get; private set; }
        public int SetCommunityBlockedCalls { get; private set; }
        public int SetCommunityActiveCalls { get; private set; }

        public Task<ExternalUserResponse?> GetUserAsync(string userId, CancellationToken cancellationToken) =>
            Task.FromResult<ExternalUserResponse?>(new ExternalUserResponse(Guid.NewGuid(), "target", _targetRole, "Active"));

        public Task<SideEffectResult> DeletePostAsync(string postId, string reason, CancellationToken cancellationToken) => Result("post", $"/api/posts/{postId}/moderation-delete");
        public Task<SideEffectResult> SetUserBlockedAsync(UserBlock block, CancellationToken cancellationToken) => Result("auth", $"/api/users/{block.BlockedUserId}/status");
        public Task<SideEffectResult> SetUserActiveAsync(string userId, CancellationToken cancellationToken)
        {
            SetActiveCalls++;
            return Result("auth", $"/api/users/{userId}/status");
        }

        public Task<SideEffectResult> SetCommunityBlockedAsync(string communityId, string moderatorUserId, string reason, CancellationToken cancellationToken)
        {
            SetCommunityBlockedCalls++;
            return Result("community", $"/internal/communities/{communityId}/status");
        }

        public Task<SideEffectResult> SetCommunityActiveAsync(string communityId, string moderatorUserId, CancellationToken cancellationToken)
        {
            SetCommunityActiveCalls++;
            return Result("community", $"/internal/communities/{communityId}/status");
        }

        public Task<SideEffectResult> NotifyPostDeletedAsync(string postId, string reason, CancellationToken cancellationToken) => Result("notifications", "/api/notifications");
        public Task<SideEffectResult> NotifyUserBlockedAsync(UserBlock block, CancellationToken cancellationToken) => Result("notifications", "/api/notifications");

        private Task<SideEffectResult> Result(string serviceName, string path)
        {
            return Task.FromResult(_fail
                ? SideEffectResult.Failed(serviceName, path, "unavailable")
                : SideEffectResult.Success(serviceName, path));
        }
    }
}
