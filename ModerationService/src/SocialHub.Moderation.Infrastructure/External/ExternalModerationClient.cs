using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using SocialHub.Moderation.Application.Abstractions;
using SocialHub.Moderation.Domain.Entities;

namespace SocialHub.Moderation.Infrastructure.External;

public sealed class ExternalModerationClient : IExternalModerationClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ExternalModerationClient> _logger;

    public ExternalModerationClient(IHttpClientFactory httpClientFactory, ILogger<ExternalModerationClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public Task DeletePostAsync(string postId, string reason, CancellationToken cancellationToken)
    {
        return TryPostAsync("post", $"/api/posts/{postId}/moderation-delete", new { reason }, cancellationToken);
    }

    public Task SetUserBlockedAsync(UserBlock block, CancellationToken cancellationToken)
    {
        return TryPostAsync("auth", $"/api/users/{block.BlockedUserId}/status", new { status = "BLOCKED", expiresAtUtc = block.ExpiresAtUtc }, cancellationToken);
    }

    public Task NotifyPostDeletedAsync(string postId, string reason, CancellationToken cancellationToken)
    {
        return TryPostAsync("notifications", "/api/notifications", new { type = "POST_DELETED", payload = new { postId, reason } }, cancellationToken);
    }

    public Task NotifyUserBlockedAsync(UserBlock block, CancellationToken cancellationToken)
    {
        return TryPostAsync("notifications", "/api/notifications", new { type = "USER_BLOCKED", recipientUserId = block.BlockedUserId, payload = block }, cancellationToken);
    }

    private async Task TryPostAsync<T>(string clientName, string path, T body, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(clientName);
        if (client.BaseAddress is null)
        {
            return;
        }

        try
        {
            await client.PostAsJsonAsync(path, body, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "External moderation side effect failed for {ClientName} {Path}", clientName, path);
        }
    }
}
