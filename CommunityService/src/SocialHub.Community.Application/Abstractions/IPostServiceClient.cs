using SocialHub.Community.Application.Models.External;

namespace SocialHub.Community.Application.Abstractions;

public interface IPostServiceClient
{
    Task<PostPublicationResult> PublishApprovedSuggestedPostAsync(PublishSuggestedPostRequest request, CancellationToken cancellationToken);
    Task<bool> DeletePostsByCommunityAsync(Guid communityId, CancellationToken cancellationToken);
}
