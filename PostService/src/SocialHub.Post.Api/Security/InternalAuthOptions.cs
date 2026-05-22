namespace SocialHub.Post.Api.Security;

public sealed class InternalAuthOptions
{
    public const string SectionName = "InternalAuth";
    public const string HeaderName = "X-Internal-Token";

    public bool RequireInternalToken { get; init; } = true;
    public string Token { get; init; } = string.Empty;
}
