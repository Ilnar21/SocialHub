using SocialHub.Community.Domain.Enums;

namespace SocialHub.Community.Application.Models.Communities;

public sealed record SetCommunityStatusRequest(
    CommunityStatus Status,
    Guid ModeratorUserId,
    string? Reason);
