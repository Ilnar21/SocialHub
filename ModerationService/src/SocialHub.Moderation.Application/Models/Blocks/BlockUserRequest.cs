namespace SocialHub.Moderation.Application.Models.Blocks;

public sealed record BlockUserRequest(int DurationDays, string Reason);
