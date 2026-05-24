using SocialHub.Community.Domain.Enums;

namespace SocialHub.Community.Application.Models.Communities;

public sealed record CommunitySummaryResponse(
    Guid Id,
    string Name,
    string Description,
    CommunityType Type,
    DateTime CreatedAtUtc,
    int MembersCount,
    CommunityMemberRole? CurrentUserRole = null);
