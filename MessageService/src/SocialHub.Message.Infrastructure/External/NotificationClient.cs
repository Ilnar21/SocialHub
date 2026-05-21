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

        if (!Guid.TryParse(request.RecipientUserId, out var recipientUserId))
        {
            _logger.LogWarning("Notification was skipped because recipient id {UserId} is not a valid GUID.", request.RecipientUserId);
            return;
        }

        var sourceEntityId = Guid.TryParse(request.MessageId, out var messageId)
            ? messageId
            : (Guid?)null;

        try
        {
            await _httpClient.PostAsJsonAsync("/api/notification-events", new
            {
                recipientUserId,
                type = 1,
                title = "Новое сообщение",
                message = $"Пользователь {request.SenderUserId} отправил вам сообщение.",
                sourceService = "MessageService",
                sourceEntityId
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Notification service is unavailable for message {MessageId}", request.MessageId);
        }
    }
}
