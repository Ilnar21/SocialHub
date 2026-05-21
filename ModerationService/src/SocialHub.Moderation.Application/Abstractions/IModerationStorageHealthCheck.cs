namespace SocialHub.Moderation.Application.Abstractions;

public interface IModerationStorageHealthCheck
{
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken);
}
