namespace SocialHub.Moderation.Application.Models.External;

public sealed record ExternalUserResponse(
    Guid Id,
    string Username,
    string Role,
    string Status);
