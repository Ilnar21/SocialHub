using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialHub.Community.Api.Security;
using SocialHub.Community.Application.Abstractions;

namespace SocialHub.Community.Api.Controllers;

[ApiController]
[AllowAnonymous]
[ServiceFilter(typeof(InternalTokenFilter))]
public sealed class InternalCommunitiesController : ControllerBase
{
    private readonly ICommunityService _communityService;

    public InternalCommunitiesController(ICommunityService communityService)
    {
        _communityService = communityService;
    }

    [HttpGet("internal/users/{userId:guid}/community-ids")]
    public async Task<ActionResult<List<Guid>>> GetUserCommunityIds(Guid userId, CancellationToken cancellationToken)
    {
        return Ok(await _communityService.GetCommunityIdsByUserAsync(userId, cancellationToken));
    }

    [HttpGet("internal/communities/{communityId:guid}/members/{userId:guid}")]
    [HttpGet("communities/{communityId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> CheckMembership(Guid communityId, Guid userId, CancellationToken cancellationToken)
    {
        var isMember = await _communityService.IsMemberAsync(communityId, userId, cancellationToken);
        return isMember ? Ok(new { communityId, userId, isMember = true }) : NotFound();
    }

    [HttpGet("internal/communities/{communityId:guid}/owners/{userId:guid}")]
    [HttpGet("communities/{communityId:guid}/owners/{userId:guid}")]
    public async Task<IActionResult> CheckOwnership(Guid communityId, Guid userId, CancellationToken cancellationToken)
    {
        var isOwner = await _communityService.IsOwnerAsync(communityId, userId, cancellationToken);
        return isOwner ? Ok(new { communityId, userId, isOwner = true }) : NotFound();
    }
}
