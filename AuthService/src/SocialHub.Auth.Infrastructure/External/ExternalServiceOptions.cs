namespace SocialHub.Auth.Infrastructure.External;

public sealed class ExternalServiceOptions
{
    public const string SectionName = "ExternalServices";

    public string? NotificationBaseUrl { get; set; }
    public string? InternalToken { get; set; }
    public int TimeoutSeconds { get; set; } = 4;
}
