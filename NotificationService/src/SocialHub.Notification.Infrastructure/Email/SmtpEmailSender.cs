using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocialHub.Notification.Application.Abstractions;
using SocialHub.Notification.Application.Models.Email;

namespace SocialHub.Notification.Infrastructure.Email;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<EmailDeliveryResult> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return EmailDeliveryResult.SkippedBecause("SMTP delivery is disabled.");
        }

        if (string.IsNullOrWhiteSpace(message.RecipientEmail))
        {
            return EmailDeliveryResult.SkippedBecause("Recipient email is not specified.");
        }

        if (!_options.HasCredentials)
        {
            return EmailDeliveryResult.SkippedBecause("SMTP credentials are incomplete.");
        }

        using var smtpClient = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.UseStartTls,
            Credentials = new NetworkCredential(_options.UserName, _options.Password)
        };

        using var mailMessage = new MailMessage
        {
            From = new MailAddress(_options.FromEmail, _options.FromName),
            Subject = message.Subject,
            Body = message.Body
        };
        mailMessage.To.Add(message.RecipientEmail);

        await smtpClient.SendMailAsync(mailMessage, cancellationToken);
        _logger.LogInformation("Email notification was sent to {RecipientEmail}.", message.RecipientEmail);

        return EmailDeliveryResult.Delivered();
    }
}
