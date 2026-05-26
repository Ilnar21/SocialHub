using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialHub.Moderation.Application.Abstractions;
using SocialHub.Moderation.Application.Models.Audit;
using SocialHub.Moderation.Application.Models.Blocks;

namespace SocialHub.Moderation.Api.Controllers;

[ApiController]
[Authorize(Roles = "PlatformModerator")]
[Route("api/communities")]
public sealed class CommunityBlocksController : ControllerBase
{
    private readonly IModerationService _moderationService;

    public CommunityBlocksController(IModerationService moderationService)
    {
        _moderationService = moderationService;
    }

    [HttpPost("{communityId}/blocks")]
    public async Task<ActionResult<AuditResponse>> BlockCommunity(
        string communityId,
        BlockCommunityRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _moderationService.BlockCommunityAsync(communityId, request, cancellationToken);
        return Created($"/api/communities/{communityId}/blocks", response);
    }

    [HttpDelete("{communityId}/blocks")]
    public async Task<IActionResult> UnblockCommunity(string communityId, CancellationToken cancellationToken)
    {
        await _moderationService.UnblockCommunityAsync(communityId, cancellationToken);
        return NoContent();
    }
}
