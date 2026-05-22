using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialHub.Moderation.Application.Abstractions;
using SocialHub.Moderation.Application.Models.Reports;

namespace SocialHub.Moderation.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly IModerationService _moderationService;

    public ReportsController(IModerationService moderationService)
    {
        _moderationService = moderationService;
    }

    [HttpPost]
    public async Task<ActionResult<ReportResponse>> CreateReport(CreateReportRequest request, CancellationToken cancellationToken)
    {
        var response = await _moderationService.CreateReportAsync(request, cancellationToken);
        return Created($"/api/reports/{response.Id}", response);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ReportResponse>>> GetReports([FromQuery] string? status, CancellationToken cancellationToken)
    {
        return Ok(await _moderationService.GetReportsAsync(status, cancellationToken));
    }

    [HttpPost("{reportId:guid}/resolve/delete-post")]
    public async Task<ActionResult<ReportResponse>> DeleteReportedPost(
        Guid reportId,
        ResolveReportRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _moderationService.DeleteReportedPostAsync(reportId, request, cancellationToken));
    }
}
