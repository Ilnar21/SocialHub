namespace SocialHub.Community.Application.Abstractions;

public interface IModerationClient
{
    Task<bool> DeleteCommunityReportsAsync(Guid communityId, CancellationToken cancellationToken);
}
