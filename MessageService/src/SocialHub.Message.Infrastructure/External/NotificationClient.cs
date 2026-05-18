using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using SocialHub.Message.Application.Abstractions;
using SocialHub.Message.Application.Models.External;

namespace SocialHub.Message.Infrastructure.External;

public sealed class NotificationClient : INotificationClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NotificationClient> _logger;

    public NotificationClient(HttpClient httpClient, ILogger<NotificationClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task NotifyMessageReceivedAsync(MessageReceivedNotification request, CancellationToken cancellationToken)
    {
        if (_httpClient.BaseAddress is null)
        {
            return;
        }

        try
        {
            await _httpClient.PostAsJsonAsync("/api/notifications", new
            {
                type = "MESSAGE_RECEIVED",
                recipientUserId = request.RecipientUserId,
                payload = new
                {
                    request.SenderUserId,
                    request.MessageId,
                    request.SentAt
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Notification service is unavailable for message {MessageId}", request.MessageId);
        }
    }
}
