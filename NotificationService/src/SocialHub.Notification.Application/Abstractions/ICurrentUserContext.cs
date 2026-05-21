namespace SocialHub.Notification.Application.Abstractions;

public interface ICurrentUserContext
{
    Guid UserId { get; }
}
