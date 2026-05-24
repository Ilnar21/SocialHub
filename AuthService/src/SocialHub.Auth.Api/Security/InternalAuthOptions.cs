namespace SocialHub.Auth.Api.Security;

public sealed class InternalAuthOptions
{
    public const string SectionName = "InternalAuth";

    public bool RequireInternalToken { get; set; } = true;
    public string Token { get; set; } = "local-dev-internal-service-token-change-me";
}
