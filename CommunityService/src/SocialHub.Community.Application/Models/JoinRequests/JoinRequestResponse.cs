using SocialHub.Community.Domain.Enums;

namespace SocialHub.Community.Application.Models.JoinRequests;

public sealed record JoinRequestResponse(
    Guid Id,
    Guid CommunityId,
    Guid UserId,
    CommunityJoinRequestStatus Status,
    DateTime CreatedAtUtc,
    Guid? ReviewedByUserId,
    DateTime? ReviewedAtUtc,
    string? ReviewComment);
