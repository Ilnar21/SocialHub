namespace SocialHub.Moderation.Application.Models.Blocks;

public sealed record BlockResponse(
    Guid Id,
    string BlockedUserId,
    string ModeratorUserId,
    string Reason,
    DateTimeOffset BlockedAtUtc,
    DateTimeOffset ExpiresAtUtc);
