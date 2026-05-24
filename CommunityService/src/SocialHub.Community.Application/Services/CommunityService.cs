using SocialHub.Community.Application.Abstractions;
using SocialHub.Community.Application.Exceptions;
using SocialHub.Community.Application.Models.Communities;
using SocialHub.Community.Application.Models.External;
using SocialHub.Community.Application.Models.Members;
using SocialHub.Community.Application.Models.SuggestedPosts;
using SocialHub.Community.Domain.Constants;
using SocialHub.Community.Domain.Entities;
using SocialHub.Community.Domain.Enums;

namespace SocialHub.Community.Application.Services;

public sealed class CommunityService : ICommunityService
{
    private readonly ICommunityRepository _repository;
    private readonly ICurrentUserContext _currentUser;
    private readonly INotificationClient _notificationClient;
    private readonly IPostServiceClient _postServiceClient;

    public CommunityService(
        ICommunityRepository repository,
        ICurrentUserContext currentUser,
        INotificationClient notificationClient,
        IPostServiceClient postServiceClient)
    {
        _repository = repository;
        _currentUser = currentUser;
        _notificationClient = notificationClient;
        _postServiceClient = postServiceClient;
    }

    public async Task<List<CommunitySummaryResponse>> GetCommunitiesAsync(CancellationToken cancellationToken)
    {
        var communities = await _repository.GetCommunitiesAsync(cancellationToken);
        return communities
            .OrderBy(c => c.Name)
            .Select(ToSummary)
            .ToList();
    }

    public async Task<List<CommunitySummaryResponse>> GetCurrentUserCommunitiesAsync(CancellationToken cancellationToken)
    {
        var communities = await _repository.GetCommunitiesByUserAsync(_currentUser.UserId, cancellationToken);
        return communities
            .OrderBy(c => c.Name)
            .Select(ToSummary)
            .ToList();
    }

    public async Task<CommunityDetailsResponse> GetCommunityAsync(Guid communityId, CancellationToken cancellationToken)
    {
        var community = await GetRequiredCommunityAsync(communityId, cancellationToken);
        var currentMembership = await _repository.GetMemberAsync(communityId, _currentUser.UserId, cancellationToken);
        return ToDetails(community, currentMembership);
    }

    public async Task<CommunityDetailsResponse> CreateCommunityAsync(CreateCommunityRequest request, CancellationToken cancellationToken)
    {
        var normalizedName = request.Name.Trim().ToUpperInvariant();
        if (await _repository.CommunityNameExistsAsync(normalizedName, cancellationToken))
        {
            throw AppException.Conflict("Community name already exists.");
        }

        var membershipCount = await _repository.CountMembershipsAsync(_currentUser.UserId, cancellationToken);
        if (membershipCount >= CommunityLimits.MaxCommunitiesPerUser)
        {
            throw AppException.Conflict("Membership limit reached: a user can join no more than 30 communities.");
        }

        var now = DateTime.UtcNow;
        var community = new Community.Domain.Entities.Community(request.Name, request.Description, request.Type, _currentUser.UserId, now);
        var owner = community.AddOwner(_currentUser.UserId, now);

        await _repository.AddCommunityAsync(community, cancellationToken);
        await _repository.AddAuditLogAsync(new CommunityAuditLog(community.Id, _currentUser.UserId, "COMMUNITY_CREATED", $"Community '{community.Name}' was created.", now), cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return ToDetails(community, owner);
    }

    public async Task<MemberResponse> JoinCommunityAsync(Guid communityId, CancellationToken cancellationToken)
    {
        var community = await GetRequiredCommunityAsync(communityId, cancellationToken);
        var existingMember = await _repository.GetMemberAsync(communityId, _currentUser.UserId, cancellationToken);
        if (existingMember is not null)
        {
            return ToMemberResponse(existingMember);
        }

        var membershipCount = await _repository.CountMembershipsAsync(_currentUser.UserId, cancellationToken);
        if (membershipCount >= CommunityLimits.MaxCommunitiesPerUser)
        {
            throw AppException.Conflict("Membership limit reached: a user can join no more than 30 communities.");
        }

        var now = DateTime.UtcNow;
        var member = community.AddMember(_currentUser.UserId, now);
        await _repository.AddMemberAsync(member, cancellationToken);
        await _repository.AddAuditLogAsync(new CommunityAuditLog(communityId, _currentUser.UserId, "MEMBER_JOINED", "User joined the community.", now), cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return ToMemberResponse(member);
    }

    public async Task LeaveCommunityAsync(Guid communityId, CancellationToken cancellationToken)
    {
        await GetRequiredCommunityAsync(communityId, cancellationToken);
        var member = await GetRequiredMemberAsync(communityId, _currentUser.UserId, cancellationToken);
        if (member.Role == CommunityMemberRole.Owner)
        {
            throw AppException.Conflict("Community owner cannot leave before transferring ownership.");
        }

        _repository.RemoveMember(member);
        await _repository.AddAuditLogAsync(new CommunityAuditLog(communityId, _currentUser.UserId, "MEMBER_LEFT", "User left the community.", DateTime.UtcNow), cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<MemberResponse>> GetMembersAsync(Guid communityId, CancellationToken cancellationToken)
    {
        await GetRequiredCommunityAsync(communityId, cancellationToken);
        var members = await _repository.GetMembersAsync(communityId, cancellationToken);
        return members
            .OrderBy(m => m.Role)
            .ThenBy(m => m.JoinedAtUtc)
            .Select(ToMemberResponse)
            .ToList();
    }

    public async Task<bool> IsMemberAsync(Guid communityId, Guid userId, CancellationToken cancellationToken)
    {
        return await _repository.IsMemberAsync(communityId, userId, cancellationToken);
    }

    public async Task<bool> IsOwnerAsync(Guid communityId, Guid userId, CancellationToken cancellationToken)
    {
        var member = await _repository.GetMemberAsync(communityId, userId, cancellationToken);
        return member?.Role == CommunityMemberRole.Owner;
    }

    public async Task<List<Guid>> GetCommunityIdsByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _repository.GetCommunityIdsByUserAsync(userId, cancellationToken);
    }

    public async Task RemoveMemberAsync(Guid communityId, Guid memberUserId, CancellationToken cancellationToken)
    {
        await EnsureCurrentUserCanAdminCommunityAsync(communityId, cancellationToken);

        if (memberUserId == _currentUser.UserId)
        {
            throw AppException.BadRequest("Administrators should use the leave community endpoint for themselves.");
        }

        var member = await GetRequiredMemberAsync(communityId, memberUserId, cancellationToken);
        if (member.Role == CommunityMemberRole.Owner)
        {
            throw AppException.Forbidden("Community owner cannot be removed.");
        }

        _repository.RemoveMember(member);
        await _repository.AddAuditLogAsync(new CommunityAuditLog(communityId, _currentUser.UserId, "MEMBER_REMOVED", $"User '{memberUserId}' was removed from community.", DateTime.UtcNow), cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<MemberResponse> ChangeMemberRoleAsync(Guid communityId, Guid memberUserId, CommunityMemberRole role, CancellationToken cancellationToken)
    {
        var actor = await GetRequiredMemberAsync(communityId, _currentUser.UserId, cancellationToken);
        if (actor.Role != CommunityMemberRole.Owner)
        {
            throw AppException.Forbidden("Only community owner can change member roles.");
        }

        if (role == CommunityMemberRole.Owner)
        {
            throw AppException.BadRequest("Ownership transfer is not supported by this endpoint.");
        }

        var member = await GetRequiredMemberAsync(communityId, memberUserId, cancellationToken);
        if (member.Role == CommunityMemberRole.Owner)
        {
            throw AppException.Forbidden("Owner role cannot be changed by this endpoint.");
        }

        member.ChangeRole(role);
        await _repository.AddAuditLogAsync(new CommunityAuditLog(communityId, _currentUser.UserId, "MEMBER_ROLE_CHANGED", $"User '{memberUserId}' role changed to '{role}'.", DateTime.UtcNow), cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return ToMemberResponse(member);
    }

    public async Task<SuggestedPostResponse> SubmitSuggestedPostAsync(Guid communityId, SubmitSuggestedPostRequest request, CancellationToken cancellationToken)
    {
        await GetRequiredCommunityAsync(communityId, cancellationToken);
        await GetRequiredMemberAsync(communityId, _currentUser.UserId, cancellationToken);

        var suggestedPost = new SuggestedPost(communityId, _currentUser.UserId, request.Title, request.Text, DateTime.UtcNow);
        await _repository.AddSuggestedPostAsync(suggestedPost, cancellationToken);
        await _repository.AddAuditLogAsync(new CommunityAuditLog(communityId, _currentUser.UserId, "SUGGESTED_POST_CREATED", $"Suggested post '{suggestedPost.Title}' was created.", suggestedPost.CreatedAtUtc), cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        var administrators = await _repository.GetMembersAsync(communityId, cancellationToken);
        foreach (var admin in administrators.Where(m => m.Role == CommunityMemberRole.Owner))
        {
            await _notificationClient.NotifyAsync(
                new InternalNotificationRequest(
                    admin.UserId,
                    "SuggestedPostCreated",
                    "New suggested post",
                    $"A new post '{suggestedPost.Title}' is waiting for review.",
                    communityId,
                    suggestedPost.Id),
                cancellationToken);
        }

        return ToSuggestedPostResponse(suggestedPost);
    }

    public async Task<List<SuggestedPostResponse>> GetSuggestedPostsAsync(Guid communityId, SuggestedPostStatus? status, CancellationToken cancellationToken)
    {
        await EnsureCurrentUserOwnsCommunityAsync(communityId, cancellationToken);
        var posts = await _repository.GetSuggestedPostsAsync(communityId, status, cancellationToken);
        return posts
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(ToSuggestedPostResponse)
            .ToList();
    }

    public async Task<SuggestedPostResponse> ApproveSuggestedPostAsync(Guid communityId, Guid suggestedPostId, CancellationToken cancellationToken)
    {
        await EnsureCurrentUserOwnsCommunityAsync(communityId, cancellationToken);
        var suggestedPost = await GetRequiredSuggestedPostAsync(communityId, suggestedPostId, cancellationToken);

        PostPublicationResult publicationResult;
        try
        {
            publicationResult = await _postServiceClient.PublishApprovedSuggestedPostAsync(
                new PublishSuggestedPostRequest(
                    communityId,
                    suggestedPost.AuthorUserId,
                    suggestedPost.Id,
                    suggestedPost.Title,
                    suggestedPost.Text),
                cancellationToken);
        }
        catch
        {
            publicationResult = PostPublicationResult.Deferred("Post Service is unavailable; publication should be retried later.");
        }

        suggestedPost.Approve(
            _currentUser.UserId,
            publicationResult.PostId,
            publicationResult.Warning,
            DateTime.UtcNow);

        await _repository.AddAuditLogAsync(new CommunityAuditLog(communityId, _currentUser.UserId, "SUGGESTED_POST_APPROVED", $"Suggested post '{suggestedPost.Id}' was approved.", DateTime.UtcNow), cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        await _notificationClient.NotifyAsync(
            new InternalNotificationRequest(
                suggestedPost.AuthorUserId,
                "SuggestedPostApproved",
                "Your suggested post was approved",
                $"Your post '{suggestedPost.Title}' was approved.",
                communityId,
                suggestedPost.Id),
            cancellationToken);

        return ToSuggestedPostResponse(suggestedPost);
    }

    public async Task<SuggestedPostResponse> RejectSuggestedPostAsync(Guid communityId, Guid suggestedPostId, RejectSuggestedPostRequest request, CancellationToken cancellationToken)
    {
        await EnsureCurrentUserOwnsCommunityAsync(communityId, cancellationToken);
        var suggestedPost = await GetRequiredSuggestedPostAsync(communityId, suggestedPostId, cancellationToken);

        suggestedPost.Reject(_currentUser.UserId, request.Comment, DateTime.UtcNow);
        await _repository.AddAuditLogAsync(new CommunityAuditLog(communityId, _currentUser.UserId, "SUGGESTED_POST_REJECTED", $"Suggested post '{suggestedPost.Id}' was rejected.", DateTime.UtcNow), cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        await _notificationClient.NotifyAsync(
            new InternalNotificationRequest(
                suggestedPost.AuthorUserId,
                "SuggestedPostRejected",
                "Your suggested post was rejected",
                $"Your post '{suggestedPost.Title}' was rejected.",
                communityId,
                suggestedPost.Id),
            cancellationToken);

        return ToSuggestedPostResponse(suggestedPost);
    }

    private async Task<Community.Domain.Entities.Community> GetRequiredCommunityAsync(Guid communityId, CancellationToken cancellationToken)
    {
        return await _repository.GetCommunityAsync(communityId, cancellationToken)
            ?? throw AppException.NotFound("Community was not found.");
    }

    private async Task<CommunityMember> GetRequiredMemberAsync(Guid communityId, Guid userId, CancellationToken cancellationToken)
    {
        return await _repository.GetMemberAsync(communityId, userId, cancellationToken)
            ?? throw AppException.NotFound("Community member was not found.");
    }

    private async Task<SuggestedPost> GetRequiredSuggestedPostAsync(Guid communityId, Guid suggestedPostId, CancellationToken cancellationToken)
    {
        return await _repository.GetSuggestedPostAsync(communityId, suggestedPostId, cancellationToken)
            ?? throw AppException.NotFound("Suggested post was not found.");
    }

    private async Task EnsureCurrentUserCanAdminCommunityAsync(Guid communityId, CancellationToken cancellationToken)
    {
        await GetRequiredCommunityAsync(communityId, cancellationToken);
        var member = await GetRequiredMemberAsync(communityId, _currentUser.UserId, cancellationToken);
        if (member.Role is not (CommunityMemberRole.Owner or CommunityMemberRole.Admin))
        {
            throw AppException.Forbidden("Insufficient community permissions.");
        }
    }

    private async Task EnsureCurrentUserOwnsCommunityAsync(Guid communityId, CancellationToken cancellationToken)
    {
        await GetRequiredCommunityAsync(communityId, cancellationToken);
        var member = await GetRequiredMemberAsync(communityId, _currentUser.UserId, cancellationToken);
        if (member.Role != CommunityMemberRole.Owner)
        {
            throw AppException.Forbidden("Only community owner can review suggested posts.");
        }
    }

    private CommunityDetailsResponse ToDetails(Community.Domain.Entities.Community community, CommunityMember? currentMembership)
    {
        return new CommunityDetailsResponse(
            community.Id,
            community.Name,
            community.Description,
            community.Type,
            community.CreatedByUserId,
            community.CreatedAtUtc,
            community.Members.Count,
            currentMembership is null ? null : ToMemberResponse(currentMembership));
    }

    private static CommunitySummaryResponse ToSummary(Community.Domain.Entities.Community community)
    {
        return new CommunitySummaryResponse(
            community.Id,
            community.Name,
            community.Description,
            community.Type,
            community.CreatedAtUtc,
            community.Members.Count);
    }

    private static MemberResponse ToMemberResponse(CommunityMember member)
    {
        return new MemberResponse(member.Id, member.CommunityId, member.UserId, member.Role, member.JoinedAtUtc);
    }

    private static SuggestedPostResponse ToSuggestedPostResponse(SuggestedPost post)
    {
        return new SuggestedPostResponse(
            post.Id,
            post.CommunityId,
            post.AuthorUserId,
            post.Title,
            post.Text,
            post.Status,
            post.ReviewedByUserId,
            post.ReviewedAtUtc,
            post.ReviewComment,
            post.PublishedPostId,
            post.PublicationWarning,
            post.CreatedAtUtc);
    }
}
