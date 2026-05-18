using Microsoft.AspNetCore.Mvc;
using SocialHub.Moderation.Application.Abstractions;
using SocialHub.Moderation.Application.Models.Audit;

namespace SocialHub.Moderation.Api.Controllers;

[ApiController]
[Route("api/audit")]
public sealed class AuditController : ControllerBase
{
    private readonly IModerationService _moderationService;

    public AuditController(IModerationService moderationService)
    {
        _moderationService = moderationService;
    }

    [HttpPost]
    public async Task<ActionResult<AuditResponse>> CreateAudit(CreateAuditRequest request, CancellationToken cancellationToken)
    {
        var response = await _moderationService.CreateAuditAsync(request, cancellationToken);
        return Created($"/api/audit/{response.Id}", response);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<AuditResponse>>> GetAudit(
        [FromQuery] string? actorUserId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        return Ok(await _moderationService.GetAuditAsync(actorUserId, from, to, cancellationToken));
    }
}
