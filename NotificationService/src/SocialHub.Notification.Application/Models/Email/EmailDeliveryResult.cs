namespace SocialHub.Notification.Application.Models.Email;

public sealed record EmailDeliveryResult(bool Sent, bool Skipped, string? Details)
{
    public static EmailDeliveryResult Delivered() => new(true, false, null);

    public static EmailDeliveryResult SkippedBecause(string details) => new(false, true, details);
}
