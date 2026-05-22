using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialHub.Moderation.Application.Abstractions;
using SocialHub.Moderation.Application.Models.Blocks;

namespace SocialHub.Moderation.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public sealed class BlocksController : ControllerBase
{
    private readonly IModerationService _moderationService;

    public BlocksController(IModerationService moderationService)
    {
        _moderationService = moderationService;
    }

    [HttpPost("{userId}/blocks")]
    public async Task<ActionResult<BlockResponse>> BlockUser(
        string userId,
        BlockUserRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _moderationService.BlockUserAsync(userId, request, cancellationToken);
        return Created($"/api/users/{response.BlockedUserId}/blocks/{response.Id}", response);
    }
}
