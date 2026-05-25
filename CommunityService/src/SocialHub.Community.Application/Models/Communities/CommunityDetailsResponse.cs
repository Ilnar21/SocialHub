using SocialHub.Community.Application.Models.Members;
using SocialHub.Community.Application.Models.JoinRequests;
using SocialHub.Community.Domain.Enums;

namespace SocialHub.Community.Application.Models.Communities;

public sealed record CommunityDetailsResponse(
    Guid Id,
    string Name,
    string Username,
    string Description,
    CommunityType Type,
    Guid CreatedByUserId,
    DateTime CreatedAtUtc,
    int MembersCount,
    MemberResponse? CurrentUserMembership,
    JoinRequestResponse? CurrentUserJoinRequest);
