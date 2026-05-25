namespace SocialHub.Community.Domain.Constants;

public static class CommunityLimits
{
    public const int MaxCommunitiesPerUser = 30;
    public const int NameMaxLength = 80;
    public const int UsernameMaxLength = 64;
    public const int DescriptionMaxLength = 500;
    public const int SuggestedPostTitleMaxLength = 120;
    public const int SuggestedPostTextMaxLength = 10_000;
}
