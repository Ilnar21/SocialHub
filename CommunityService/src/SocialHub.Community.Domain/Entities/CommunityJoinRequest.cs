using SocialHub.Community.Domain.Enums;

namespace SocialHub.Community.Domain.Entities;

public sealed class CommunityJoinRequest
{
    private CommunityJoinRequest()
    {
    }

    public CommunityJoinRequest(Guid communityId, Guid userId, DateTime createdAtUtc)
    {
        Id = Guid.NewGuid();
        CommunityId = communityId;
        UserId = userId;
        Status = CommunityJoinRequestStatus.Pending;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid CommunityId { get; private set; }
    public Guid UserId { get; private set; }
    public CommunityJoinRequestStatus Status { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public string? ReviewComment { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public Community? Community { get; private set; }

    public void Approve(Guid reviewerUserId, DateTime reviewedAtUtc)
    {
        EnsurePending();
        Status = CommunityJoinRequestStatus.Approved;
        ReviewedByUserId = reviewerUserId;
        ReviewedAtUtc = reviewedAtUtc;
    }

    public void Reject(Guid reviewerUserId, string? comment, DateTime reviewedAtUtc)
    {
        EnsurePending();
        Status = CommunityJoinRequestStatus.Rejected;
        ReviewedByUserId = reviewerUserId;
        ReviewedAtUtc = reviewedAtUtc;
        ReviewComment = NormalizeOptional(comment, 500);
    }

    private void EnsurePending()
    {
        if (Status != CommunityJoinRequestStatus.Pending)
        {
            throw new InvalidOperationException("Join request has already been reviewed.");
        }
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length > maxLength ? normalized[..maxLength] : normalized;
    }
}
