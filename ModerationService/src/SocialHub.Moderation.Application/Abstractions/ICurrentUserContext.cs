namespace SocialHub.Moderation.Application.Abstractions;

public interface ICurrentUserContext
{
    string UserId { get; }
    string? PlatformRole { get; }
}
