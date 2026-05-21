using SocialHub.Community.Domain.Entities;
using SocialHub.Community.Domain.Enums;

namespace SocialHub.Community.Application.Abstractions;

public interface ICommunityRepository
{
    Task<List<Community.Domain.Entities.Community>> GetCommunitiesAsync(CancellationToken cancellationToken);
    Task<Community.Domain.Entities.Community?> GetCommunityAsync(Guid communityId, CancellationToken cancellationToken);
    Task<bool> CommunityNameExistsAsync(string normalizedName, CancellationToken cancellationToken);
    Task<CommunityMember?> GetMemberAsync(Guid communityId, Guid userId, CancellationToken cancellationToken);
    Task<List<CommunityMember>> GetMembersAsync(Guid communityId, CancellationToken cancellationToken);
    Task<int> CountMembershipsAsync(Guid userId, CancellationToken cancellationToken);
    Task<SuggestedPost?> GetSuggestedPostAsync(Guid communityId, Guid suggestedPostId, CancellationToken cancellationToken);
    Task<List<SuggestedPost>> GetSuggestedPostsAsync(Guid communityId, SuggestedPostStatus? status, CancellationToken cancellationToken);
    Task AddCommunityAsync(Community.Domain.Entities.Community community, CancellationToken cancellationToken);
    Task AddSuggestedPostAsync(SuggestedPost suggestedPost, CancellationToken cancellationToken);
    Task AddAuditLogAsync(CommunityAuditLog auditLog, CancellationToken cancellationToken);
    void RemoveMember(CommunityMember member);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
