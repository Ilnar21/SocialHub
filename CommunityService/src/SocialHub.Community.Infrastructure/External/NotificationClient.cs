using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocialHub.Community.Application.Abstractions;
using SocialHub.Community.Application.Models.External;

namespace SocialHub.Community.Infrastructure.External;

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

    public async Task<bool> NotifyAsync(InternalNotificationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.NotificationBaseUrl))
        {
            _logger.LogInformation("Notification Service is not configured. Notification {Type} for user {UserId} was skipped by stub.", request.Type, request.RecipientUserId);
            return true;
        }

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/notifications/internal", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            _logger.LogWarning("Notification Service returned {StatusCode} for notification {Type}.", response.StatusCode, request.Type);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Notification Service is unavailable. Business operation remains successful.");
            return false;
        }
    }
}
