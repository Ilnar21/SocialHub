namespace SocialHub.Community.Application.Models.External;

public sealed record InternalNotificationRequest(
    Guid RecipientUserId,
    string Type,
    string Title,
    string Message,
    Guid? SourceCommunityId = null,
    Guid? SourceEntityId = null);
