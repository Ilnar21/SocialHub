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
        return await SendAsync("/api/notifications/internal", request, cancellationToken);
    }

    public async Task<bool> NotifyEventAsync(InternalNotificationRequest request, CancellationToken cancellationToken)
    {
        return await SendAsync("/api/notification-events", new
        {
            request.RecipientUserId,
            request.Type,
            request.Title,
            request.Message,
            sourceService = "CommunityService",
            sourceEntityId = request.SourceEntityId ?? request.SourceCommunityId
        }, cancellationToken);
    }

    private async Task<bool> SendAsync<T>(string path, T request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.NotificationBaseUrl))
        {
            _logger.LogInformation("Notification Service is not configured. Notification request to {Path} was skipped by stub.", path);
            return true;
        }

        try
        {
            var response = await _httpClient.PostAsJsonAsync(path, request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            _logger.LogWarning("Notification Service returned {StatusCode} for request {Path}.", response.StatusCode, path);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Notification Service is unavailable. Business operation remains successful.");
            return false;
        }
    }
}
