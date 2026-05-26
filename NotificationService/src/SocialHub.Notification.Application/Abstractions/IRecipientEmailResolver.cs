namespace SocialHub.Notification.Application.Abstractions;

public interface IRecipientEmailResolver
{
    Task<string?> ResolveEmailAsync(Guid userId, CancellationToken cancellationToken);
}
