using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using SocialHub.Moderation.Api.Controllers;
using SocialHub.Moderation.Application.Abstractions;
using SocialHub.Moderation.Application.Models.Audit;
using SocialHub.Moderation.Application.Models.Blocks;
using SocialHub.Moderation.Application.Models.Reports;

namespace SocialHub.Moderation.Tests;

[TestFixture]
public sealed class ModerationControllerTests
{
    [Test]
    public async Task CreateReport_returns_created_response()
    {
        var controller = new ReportsController(new FakeModerationService());

        var result = await controller.CreateReport(new CreateReportRequest("POST", "post-1", "Спам", null), CancellationToken.None);

        Assert.That(result.Result, Is.TypeOf<CreatedResult>());
    }

    [Test]
    public async Task BlockUser_returns_created_response()
    {
        var controller = new BlocksController(new FakeModerationService());

        var result = await controller.BlockUser("ivan.petrov", new BlockUserRequest(7, "Спам"), CancellationToken.None);

        Assert.That(result.Result, Is.TypeOf<CreatedResult>());
    }

    [Test]
    public void PrivateMessages_endpoint_is_forbidden()
    {
        var controller = new PrivateMessagesController();

        var result = controller.DenyPrivateMessages();

        Assert.That(result, Is.TypeOf<ObjectResult>());
        Assert.That(((ObjectResult)result).StatusCode, Is.EqualTo(403));
    }

    private sealed class FakeModerationService : IModerationService
    {
        public Task<ReportResponse> CreateReportAsync(CreateReportRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(NewReport("NEW"));
        }

        public Task<IReadOnlyCollection<ReportResponse>> GetReportsAsync(string? status, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<ReportResponse>>(new[] { NewReport("NEW") });
        }

        public Task<ReportResponse> DeleteReportedPostAsync(Guid reportId, ResolveReportRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(NewReport("RESOLVED"));
        }

        public Task<BlockResponse> BlockUserAsync(string userId, BlockUserRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new BlockResponse(Guid.NewGuid(), userId, "pavel.mod", request.Reason, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(request.DurationDays)));
        }

        public Task<AuditResponse> CreateAuditAsync(CreateAuditRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AuditResponse(Guid.NewGuid(), "pavel.mod", request.ActorRole ?? "UNKNOWN", request.Action, request.TargetType, request.TargetId, request.CommunityId, request.Reason ?? "", DateTimeOffset.UtcNow));
        }

        public Task<IReadOnlyCollection<AuditResponse>> GetAuditAsync(string? actorUserId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<AuditResponse>>(Array.Empty<AuditResponse>());
        }

        private static ReportResponse NewReport(string status)
        {
            return new ReportResponse(Guid.NewGuid(), "ivan.petrov", "POST", "post-1", "Спам", null, status, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        }
    }
}
