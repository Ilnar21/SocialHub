using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SocialHub.Community.Application.Abstractions;
using SocialHub.Community.Application.Models.Communities;
using SocialHub.Community.Application.Models.JoinRequests;
using SocialHub.Community.Application.Models.Members;
using SocialHub.Community.Application.Models.SuggestedPosts;
using SocialHub.Community.Domain.Enums;

namespace SocialHub.Community.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/communities")]
public sealed class CommunitiesController : ControllerBase
{
    private readonly ICommunityService _communityService;

    public CommunitiesController(ICommunityService communityService)
    {
        _communityService = communityService;
    }

    [HttpGet]
    public async Task<ActionResult<List<CommunitySummaryResponse>>> GetCommunities(CancellationToken cancellationToken)
    {
        return Ok(await _communityService.GetCommunitiesAsync(cancellationToken));
    }

    [HttpGet("my")]
    public async Task<ActionResult<List<CommunitySummaryResponse>>> GetMyCommunities(CancellationToken cancellationToken)
    {
        return Ok(await _communityService.GetCurrentUserCommunitiesAsync(cancellationToken));
    }

    [HttpGet("blocked")]
    [Authorize(Roles = "PlatformModerator")]
    public async Task<ActionResult<List<CommunitySummaryResponse>>> GetBlockedCommunities(CancellationToken cancellationToken)
    {
        return Ok(await _communityService.GetBlockedCommunitiesAsync(cancellationToken));
    }

    [HttpGet("{communityId:guid}")]
    public async Task<ActionResult<CommunityDetailsResponse>> GetCommunity(Guid communityId, CancellationToken cancellationToken)
    {
        return Ok(await _communityService.GetCommunityAsync(communityId, cancellationToken));
    }

    [HttpGet("by-username/{username}")]
    public async Task<ActionResult<CommunityDetailsResponse>> GetCommunityByUsername(string username, CancellationToken cancellationToken)
    {
        return Ok(await _communityService.GetCommunityByUsernameAsync(username, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<CommunityDetailsResponse>> CreateCommunity(CreateCommunityRequest request, CancellationToken cancellationToken)
    {
        var response = await _communityService.CreateCommunityAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetCommunity), new { communityId = response.Id }, response);
    }

    [HttpPut("{communityId:guid}")]
    public async Task<ActionResult<CommunityDetailsResponse>> UpdateCommunity(
        Guid communityId,
        UpdateCommunityRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _communityService.UpdateCommunityAsync(communityId, request, cancellationToken));
    }

    [HttpDelete("{communityId:guid}")]
    public async Task<IActionResult> DeleteCommunity(Guid communityId, CancellationToken cancellationToken)
    {
        await _communityService.DeleteCommunityAsync(communityId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{communityId:guid}/join")]
    public async Task<ActionResult<MemberResponse>> JoinCommunity(Guid communityId, CancellationToken cancellationToken)
    {
        return Ok(await _communityService.JoinCommunityAsync(communityId, cancellationToken));
    }

    [HttpPost("{communityId:guid}/join-requests")]
    public async Task<ActionResult<JoinRequestResponse>> RequestToJoinCommunity(Guid communityId, CancellationToken cancellationToken)
    {
        var response = await _communityService.RequestToJoinCommunityAsync(communityId, cancellationToken);
        return CreatedAtAction(nameof(GetJoinRequests), new { communityId, status = response.Status }, response);
    }

    [HttpDelete("{communityId:guid}/membership")]
    public async Task<IActionResult> LeaveCommunity(Guid communityId, CancellationToken cancellationToken)
    {
        await _communityService.LeaveCommunityAsync(communityId, cancellationToken);
        return NoContent();
    }

    [HttpGet("{communityId:guid}/members")]
    public async Task<ActionResult<List<MemberResponse>>> GetMembers(Guid communityId, CancellationToken cancellationToken)
    {
        return Ok(await _communityService.GetMembersAsync(communityId, cancellationToken));
    }

    [HttpGet("{communityId:guid}/join-requests")]
    public async Task<ActionResult<List<JoinRequestResponse>>> GetJoinRequests(
        Guid communityId,
        [FromQuery] CommunityJoinRequestStatus? status,
        CancellationToken cancellationToken)
    {
        return Ok(await _communityService.GetJoinRequestsAsync(communityId, status, cancellationToken));
    }

    [HttpPost("{communityId:guid}/join-requests/{requestId:guid}/approve")]
    public async Task<ActionResult<JoinRequestResponse>> ApproveJoinRequest(
        Guid communityId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        return Ok(await _communityService.ApproveJoinRequestAsync(communityId, requestId, cancellationToken));
    }

    [HttpPost("{communityId:guid}/join-requests/{requestId:guid}/reject")]
    public async Task<ActionResult<JoinRequestResponse>> RejectJoinRequest(
        Guid communityId,
        Guid requestId,
        RejectJoinRequestRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _communityService.RejectJoinRequestAsync(communityId, requestId, request, cancellationToken));
    }

    [HttpDelete("{communityId:guid}/members/{memberUserId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid communityId, Guid memberUserId, CancellationToken cancellationToken)
    {
        await _communityService.RemoveMemberAsync(communityId, memberUserId, cancellationToken);
        return NoContent();
    }

    [HttpPut("{communityId:guid}/members/{memberUserId:guid}/role")]
    public async Task<ActionResult<MemberResponse>> ChangeMemberRole(
        Guid communityId,
        Guid memberUserId,
        ChangeMemberRoleRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _communityService.ChangeMemberRoleAsync(communityId, memberUserId, request.Role, cancellationToken));
    }

    [HttpPost("{communityId:guid}/suggested-posts")]
    public async Task<ActionResult<SuggestedPostResponse>> SubmitSuggestedPost(
        Guid communityId,
        SubmitSuggestedPostRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _communityService.SubmitSuggestedPostAsync(communityId, request, cancellationToken);
        return CreatedAtAction(nameof(GetSuggestedPosts), new { communityId, status = response.Status }, response);
    }

    [HttpGet("{communityId:guid}/suggested-posts")]
    public async Task<ActionResult<List<SuggestedPostResponse>>> GetSuggestedPosts(
        Guid communityId,
        [FromQuery] SuggestedPostStatus? status,
        CancellationToken cancellationToken)
    {
        return Ok(await _communityService.GetSuggestedPostsAsync(communityId, status, cancellationToken));
    }

    [HttpPost("{communityId:guid}/suggested-posts/{suggestedPostId:guid}/approve")]
    public async Task<ActionResult<SuggestedPostResponse>> ApproveSuggestedPost(
        Guid communityId,
        Guid suggestedPostId,
        CancellationToken cancellationToken)
    {
        return Ok(await _communityService.ApproveSuggestedPostAsync(communityId, suggestedPostId, cancellationToken));
    }

    [HttpPost("{communityId:guid}/suggested-posts/{suggestedPostId:guid}/reject")]
    public async Task<ActionResult<SuggestedPostResponse>> RejectSuggestedPost(
        Guid communityId,
        Guid suggestedPostId,
        RejectSuggestedPostRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _communityService.RejectSuggestedPostAsync(communityId, suggestedPostId, request, cancellationToken));
    }
}
