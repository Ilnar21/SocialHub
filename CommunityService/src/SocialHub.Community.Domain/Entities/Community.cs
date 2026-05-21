using SocialHub.Community.Domain.Constants;
using SocialHub.Community.Domain.Enums;

namespace SocialHub.Community.Domain.Entities;

public sealed class Community
{
    private readonly List<CommunityMember> _members = [];
    private readonly List<SuggestedPost> _suggestedPosts = [];

    private Community()
    {
    }

    public Community(string name, string description, CommunityType type, Guid createdByUserId, DateTime createdAtUtc)
    {
        Id = Guid.NewGuid();
        Name = NormalizeRequired(name, nameof(name), CommunityLimits.NameMaxLength);
        NormalizedName = Name.ToUpperInvariant();
        Description = NormalizeOptional(description, CommunityLimits.DescriptionMaxLength);
        Type = type;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public CommunityType Type { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<CommunityMember> Members => _members;
    public IReadOnlyCollection<SuggestedPost> SuggestedPosts => _suggestedPosts;

    public CommunityMember AddOwner(Guid userId, DateTime joinedAtUtc)
    {
        var member = new CommunityMember(Id, userId, CommunityMemberRole.Owner, joinedAtUtc);
        _members.Add(member);
        return member;
    }

    public CommunityMember AddMember(Guid userId, DateTime joinedAtUtc)
    {
        var member = new CommunityMember(Id, userId, CommunityMemberRole.Member, joinedAtUtc);
        _members.Add(member);
        UpdatedAtUtc = joinedAtUtc;
        return member;
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

    private static string NormalizeOptional(string? value, int maxLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        return normalized.Length > maxLength ? normalized[..maxLength] : normalized;
    }
}
