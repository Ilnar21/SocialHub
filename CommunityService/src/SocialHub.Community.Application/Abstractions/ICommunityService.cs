using SocialHub.Community.Application.Models.Communities;
using SocialHub.Community.Application.Models.Members;
using SocialHub.Community.Application.Models.SuggestedPosts;
using SocialHub.Community.Domain.Enums;

namespace SocialHub.Community.Application.Abstractions;

public interface ICommunityService
{
    Task<List<CommunitySummaryResponse>> GetCommunitiesAsync(CancellationToken cancellationToken);
    Task<List<CommunitySummaryResponse>> GetCurrentUserCommunitiesAsync(CancellationToken cancellationToken);
    Task<CommunityDetailsResponse> GetCommunityAsync(Guid communityId, CancellationToken cancellationToken);
    Task<CommunityDetailsResponse> CreateCommunityAsync(CreateCommunityRequest request, CancellationToken cancellationToken);
    Task<MemberResponse> JoinCommunityAsync(Guid communityId, CancellationToken cancellationToken);
    Task LeaveCommunityAsync(Guid communityId, CancellationToken cancellationToken);
    Task<List<MemberResponse>> GetMembersAsync(Guid communityId, CancellationToken cancellationToken);
    Task<bool> IsMemberAsync(Guid communityId, Guid userId, CancellationToken cancellationToken);
    Task<List<Guid>> GetCommunityIdsByUserAsync(Guid userId, CancellationToken cancellationToken);
    Task RemoveMemberAsync(Guid communityId, Guid memberUserId, CancellationToken cancellationToken);
    Task<MemberResponse> ChangeMemberRoleAsync(Guid communityId, Guid memberUserId, CommunityMemberRole role, CancellationToken cancellationToken);
    Task<SuggestedPostResponse> SubmitSuggestedPostAsync(Guid communityId, SubmitSuggestedPostRequest request, CancellationToken cancellationToken);
    Task<List<SuggestedPostResponse>> GetSuggestedPostsAsync(Guid communityId, SuggestedPostStatus? status, CancellationToken cancellationToken);
    Task<SuggestedPostResponse> ApproveSuggestedPostAsync(Guid communityId, Guid suggestedPostId, CancellationToken cancellationToken);
    Task<SuggestedPostResponse> RejectSuggestedPostAsync(Guid communityId, Guid suggestedPostId, RejectSuggestedPostRequest request, CancellationToken cancellationToken);
}
