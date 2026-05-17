using SocialHub.Community.Domain.Constants;
using SocialHub.Community.Domain.Enums;

namespace SocialHub.Community.Domain.Entities;

public sealed class SuggestedPost
{
    private SuggestedPost()
    {
    }

    public SuggestedPost(Guid communityId, Guid authorUserId, string title, string text, DateTime createdAtUtc)
    {
        Id = Guid.NewGuid();
        CommunityId = communityId;
        AuthorUserId = authorUserId;
        Title = NormalizeRequired(title, nameof(title), CommunityLimits.SuggestedPostTitleMaxLength);
        Text = NormalizeRequired(text, nameof(text), CommunityLimits.SuggestedPostTextMaxLength);
        Status = SuggestedPostStatus.Pending;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid CommunityId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Text { get; private set; } = string.Empty;
    public SuggestedPostStatus Status { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public string? ReviewComment { get; private set; }
    public Guid? PublishedPostId { get; private set; }
    public string? PublicationWarning { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public Community? Community { get; private set; }

    public void Approve(Guid reviewerUserId, Guid? publishedPostId, string? publicationWarning, DateTime reviewedAtUtc)
    {
        EnsurePending();
        Status = SuggestedPostStatus.Approved;
        ReviewedByUserId = reviewerUserId;
        ReviewedAtUtc = reviewedAtUtc;
        PublishedPostId = publishedPostId;
        PublicationWarning = publicationWarning;
    }

    public void Reject(Guid reviewerUserId, string? comment, DateTime reviewedAtUtc)
    {
        EnsurePending();
        Status = SuggestedPostStatus.Rejected;
        ReviewedByUserId = reviewerUserId;
        ReviewedAtUtc = reviewedAtUtc;
        ReviewComment = comment;
    }

    private void EnsurePending()
    {
        if (Status != SuggestedPostStatus.Pending)
        {
            throw new InvalidOperationException("Suggested post has already been reviewed.");
        }
    }

    private static string NormalizeRequired(string value, string parameterName, int maxLength)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Value cannot be empty.", parameterName);
        }

        return normalized.Length > maxLength ? normalized[..maxLength] : normalized;
    }
}
