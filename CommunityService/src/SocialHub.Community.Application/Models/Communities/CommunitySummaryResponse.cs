using SocialHub.Community.Application.Models.JoinRequests;
using SocialHub.Community.Domain.Enums;

namespace SocialHub.Community.Application.Models.Communities;

public sealed record CommunitySummaryResponse(
    Guid Id,
    string Name,
    string Username,
    string Description,
    CommunityType Type,
    CommunityStatus Status,
    string? BlockReason,
    DateTime? BlockedAtUtc,
    DateTime CreatedAtUtc,
    int MembersCount,
    CommunityMemberRole? CurrentUserRole = null,
    JoinRequestResponse? CurrentUserJoinRequest = null);
