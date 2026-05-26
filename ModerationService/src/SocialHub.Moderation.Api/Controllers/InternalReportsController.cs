using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialHub.Moderation.Api.Security;
using SocialHub.Moderation.Application.Abstractions;

namespace SocialHub.Moderation.Api.Controllers;

[ApiController]
[AllowAnonymous]
[ServiceFilter(typeof(InternalTokenFilter))]
[Route("internal/reports")]
public sealed class InternalReportsController : ControllerBase
{
    private readonly IModerationService _moderationService;

    public InternalReportsController(IModerationService moderationService)
    {
        _moderationService = moderationService;
    }

    [HttpDelete("{targetType}/{targetId}")]
    public async Task<ActionResult<DeleteReportsResponse>> DeleteReportsByTarget(
        string targetType,
        string targetId,
        CancellationToken cancellationToken)
    {
        var deleted = await _moderationService.DeleteReportsByTargetAsync(targetType, targetId, cancellationToken);
        return Ok(new DeleteReportsResponse(deleted));
    }
}

public sealed record DeleteReportsResponse(int DeletedReports);
