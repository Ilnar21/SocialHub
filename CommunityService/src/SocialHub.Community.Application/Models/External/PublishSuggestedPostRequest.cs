namespace SocialHub.Community.Application.Models.External;

public sealed record PublishSuggestedPostRequest(
    Guid CommunityId,
    Guid AuthorUserId,
    Guid SuggestedPostId,
    string Title,
    string Text);
