namespace SocialHub.Notification.Application.Models.Email;

public sealed record EmailMessage(
    string? RecipientEmail,
    string Subject,
    string Body);
