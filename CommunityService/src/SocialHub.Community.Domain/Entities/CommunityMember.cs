using SocialHub.Community.Domain.Enums;

namespace SocialHub.Community.Domain.Entities;

public sealed class CommunityMember
{
    private CommunityMember()
    {
    }

    public CommunityMember(Guid communityId, Guid userId, CommunityMemberRole role, DateTime joinedAtUtc)
    {
        Id = Guid.NewGuid();
        CommunityId = communityId;
        UserId = userId;
        Role = role;
        JoinedAtUtc = joinedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid CommunityId { get; private set; }
    public Guid UserId { get; private set; }
    public CommunityMemberRole Role { get; private set; }
    public DateTime JoinedAtUtc { get; private set; }

    public Community? Community { get; private set; }

    public void ChangeRole(CommunityMemberRole role)
    {
        Role = role;
    }
}
