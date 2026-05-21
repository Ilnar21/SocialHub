using SocialHub.Notification.Application.Models.Email;

namespace SocialHub.Notification.Application.Abstractions;

public interface IEmailSender
{
    Task<EmailDeliveryResult> SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
