namespace SocialHub.Community.Application.Abstractions;

public interface ICurrentUserContext
{
    Guid UserId { get; }
    string? PlatformRole { get; }
}
