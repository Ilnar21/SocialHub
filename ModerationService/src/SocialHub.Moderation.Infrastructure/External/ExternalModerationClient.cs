using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using SocialHub.Moderation.Application.Abstractions;
using SocialHub.Moderation.Application.Models.External;
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

    public Task<SideEffectResult> DeletePostAsync(string postId, string reason, CancellationToken cancellationToken)
    {
        return TryPostAsync("post", $"/api/posts/{postId}/moderation-delete", new { reason }, cancellationToken);
    }

    public Task<SideEffectResult> SetUserBlockedAsync(UserBlock block, CancellationToken cancellationToken)
    {
        return TryPostAsync("auth", $"/api/users/{block.BlockedUserId}/status", new { status = "BLOCKED", expiresAtUtc = block.ExpiresAtUtc }, cancellationToken);
    }

    public Task<SideEffectResult> NotifyPostDeletedAsync(string postId, string reason, CancellationToken cancellationToken)
    {
        return TryPostAsync("notifications", "/api/notifications", new { type = "POST_DELETED", payload = new { postId, reason } }, cancellationToken);
    }

    public Task<SideEffectResult> NotifyUserBlockedAsync(UserBlock block, CancellationToken cancellationToken)
    {
        return TryPostAsync("notifications", "/api/notifications", new { type = "USER_BLOCKED", recipientUserId = block.BlockedUserId, payload = block }, cancellationToken);
    }

    private async Task<SideEffectResult> TryPostAsync<T>(string clientName, string path, T body, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(clientName);
        if (client.BaseAddress is null)
        {
            return SideEffectResult.Success(clientName, path);
        }

        try
        {
            await client.PostAsJsonAsync(path, body, cancellationToken);
            return SideEffectResult.Success(clientName, path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "External moderation side effect failed for {ClientName} {Path}", clientName, path);
            return SideEffectResult.Failed(clientName, path, ex.Message);
        }
    }
}
