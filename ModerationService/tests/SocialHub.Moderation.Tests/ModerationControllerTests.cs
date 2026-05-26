using Microsoft.AspNetCore.Authorization;
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
    public async Task UnblockUser_returns_no_content_response()
    {
        var controller = new BlocksController(new FakeModerationService());

        var result = await controller.UnblockUser("ivan.petrov", CancellationToken.None);

        Assert.That(result, Is.TypeOf<NoContentResult>());
    }

    [Test]
    public async Task BlockCommunity_returns_created_response()
    {
        var controller = new CommunityBlocksController(new FakeModerationService());

        var result = await controller.BlockCommunity("community-1", new BlockCommunityRequest("spam"), CancellationToken.None);

        Assert.That(result.Result, Is.TypeOf<CreatedResult>());
    }

    [Test]
    public async Task UnblockCommunity_returns_no_content_response()
    {
        var controller = new CommunityBlocksController(new FakeModerationService());

        var result = await controller.UnblockCommunity("community-1", CancellationToken.None);

        Assert.That(result, Is.TypeOf<NoContentResult>());
    }

    [Test]
    public void PrivateMessages_endpoint_is_forbidden()
    {
        var controller = new PrivateMessagesController();

        var result = controller.DenyPrivateMessages();

        Assert.That(result, Is.TypeOf<ObjectResult>());
        Assert.That(((ObjectResult)result).StatusCode, Is.EqualTo(403));
    }

    [Test]
    public void ReportsController_requires_jwt_and_moderator_role_for_review_actions()
    {
        AssertHasAuthorizeAttribute<ReportsController>();
        AssertMethodRequiresRole<ReportsController>(nameof(ReportsController.GetReports), "PlatformModerator");
        AssertMethodRequiresRole<ReportsController>(nameof(ReportsController.DeleteReportedPost), "PlatformModerator");
        AssertMethodRequiresRole<ReportsController>(nameof(ReportsController.ResolveReport), "PlatformModerator");
    }

    [Test]
    public void Moderator_only_controllers_require_platform_moderator_role()
    {
        AssertHasAuthorizeAttribute<BlocksController>("PlatformModerator");
        AssertHasAuthorizeAttribute<CommunityBlocksController>("PlatformModerator");
        AssertHasAuthorizeAttribute<AuditController>("PlatformModerator");
    }

    [Test]
    public void PrivateMessagesController_requires_jwt_authorization()
    {
        AssertHasAuthorizeAttribute<PrivateMessagesController>();
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

        public Task<ReportResponse> ResolveReportAsync(Guid reportId, ResolveReportRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(NewReport("RESOLVED"));
        }

        public Task<int> DeleteReportsByTargetAsync(string targetType, string targetId, CancellationToken cancellationToken)
        {
            return Task.FromResult(1);
        }

        public Task<BlockResponse> BlockUserAsync(string userId, BlockUserRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new BlockResponse(Guid.NewGuid(), userId, "pavel.mod", request.Reason, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(request.DurationDays)));
        }

        public Task<AuditResponse> UnblockUserAsync(string userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AuditResponse(Guid.NewGuid(), "pavel.mod", "PLATFORM_MODERATOR", "USER_UNBLOCKED", "USER", userId, null, "User unblocked by platform moderator.", DateTimeOffset.UtcNow));
        }

        public Task<AuditResponse> BlockCommunityAsync(string communityId, BlockCommunityRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AuditResponse(Guid.NewGuid(), "pavel.mod", "PLATFORM_MODERATOR", "COMMUNITY_BLOCKED", "COMMUNITY", communityId, communityId, request.Reason, DateTimeOffset.UtcNow));
        }

        public Task<AuditResponse> UnblockCommunityAsync(string communityId, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AuditResponse(Guid.NewGuid(), "pavel.mod", "PLATFORM_MODERATOR", "COMMUNITY_UNBLOCKED", "COMMUNITY", communityId, communityId, "Community unblocked by platform moderator.", DateTimeOffset.UtcNow));
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

    private static void AssertHasAuthorizeAttribute<TController>(string? expectedRole = null)
    {
        var authorize = Attribute.GetCustomAttribute(typeof(TController), typeof(AuthorizeAttribute)) as AuthorizeAttribute;

        Assert.That(authorize, Is.Not.Null);
        Assert.That(authorize!.Roles, Is.EqualTo(expectedRole));
    }

    private static void AssertMethodRequiresRole<TController>(string methodName, string expectedRole)
    {
        var method = typeof(TController).GetMethod(methodName)
            ?? throw new InvalidOperationException($"Method {methodName} was not found.");
        var authorize = Attribute.GetCustomAttribute(method, typeof(AuthorizeAttribute)) as AuthorizeAttribute;

        Assert.That(authorize, Is.Not.Null);
        Assert.That(authorize!.Roles, Is.EqualTo(expectedRole));
    }
}
