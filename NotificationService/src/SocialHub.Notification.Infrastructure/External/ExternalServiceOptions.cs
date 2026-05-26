namespace SocialHub.Notification.Infrastructure.External;

public sealed class ExternalServiceOptions
{
    public const string SectionName = "ExternalServices";

    public string? AuthBaseUrl { get; set; }
    public int TimeoutSeconds { get; set; } = 4;
}
