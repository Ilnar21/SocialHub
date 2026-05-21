using SocialHub.Community.Domain.Enums;

namespace SocialHub.Community.Application.Models.Members;

public sealed record MemberResponse(
    Guid Id,
    Guid CommunityId,
    Guid UserId,
    CommunityMemberRole Role,
    DateTime JoinedAtUtc);
