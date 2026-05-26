using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocialHub.Auth.Application.Abstractions;

namespace SocialHub.Auth.Infrastructure.External;

public sealed class NotificationClient : INotificationClient
{
    private readonly HttpClient _httpClient;
    private readonly ExternalServiceOptions _options;
    private readonly ILogger<NotificationClient> _logger;

    public NotificationClient(HttpClient httpClient, IOptions<ExternalServiceOptions> options, ILogger<NotificationClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task NotifyAsync(
        Guid recipientUserId,
        string recipientEmail,
        string type,
        string title,
        string message,
        Guid? sourceEntityId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.NotificationBaseUrl))
        {
            _logger.LogInformation("Notification Service is not configured. Email event {Type} for user {UserId} was skipped by stub.", type, recipientUserId);
            return;
        }

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/notification-events", new
            {
                recipientUserId,
                type,
                title,
                message,
                sourceService = "AuthService",
                sourceEntityId,
                recipientEmail
            }, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Notification Service returned {StatusCode} for auth event {Type}.", response.StatusCode, type);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Notification Service is unavailable. Auth operation remains successful.");
        }
    }
}
