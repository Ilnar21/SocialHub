namespace SocialHub.Community.Api.Security;

public sealed class DevAuthOptions
{
    public const string SectionName = "DevAuth";

    public bool EnableHeaderFallback { get; init; }
}
