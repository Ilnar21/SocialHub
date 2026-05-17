using SocialHub.Community.Domain.Enums;

namespace SocialHub.Community.Application.Models.SuggestedPosts;

public sealed record SuggestedPostResponse(
    Guid Id,
    Guid CommunityId,
    Guid AuthorUserId,
    string Title,
    string Text,
    SuggestedPostStatus Status,
    Guid? ReviewedByUserId,
    DateTime? ReviewedAtUtc,
    string? ReviewComment,
    Guid? PublishedPostId,
    string? PublicationWarning,
    DateTime CreatedAtUtc);
