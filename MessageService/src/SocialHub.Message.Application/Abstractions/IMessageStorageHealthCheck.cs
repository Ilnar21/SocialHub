namespace SocialHub.Message.Application.Abstractions;

public interface IMessageStorageHealthCheck
{
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken);
}
