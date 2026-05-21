using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SocialHub.Community.Application.Abstractions;
using SocialHub.Community.Application.Models.Communities;
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

    [HttpGet("{communityId:guid}")]
    public async Task<ActionResult<CommunityDetailsResponse>> GetCommunity(Guid communityId, CancellationToken cancellationToken)
    {
        return Ok(await _communityService.GetCommunityAsync(communityId, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<CommunityDetailsResponse>> CreateCommunity(CreateCommunityRequest request, CancellationToken cancellationToken)
    {
        var response = await _communityService.CreateCommunityAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetCommunity), new { communityId = response.Id }, response);
    }

    [HttpPost("{communityId:guid}/join")]
    public async Task<ActionResult<MemberResponse>> JoinCommunity(Guid communityId, CancellationToken cancellationToken)
    {
        return Ok(await _communityService.JoinCommunityAsync(communityId, cancellationToken));
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
