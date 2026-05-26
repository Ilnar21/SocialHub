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

    public async Task<ExternalUserResponse?> GetUserAsync(string userId, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("auth");
        if (client.BaseAddress is null)
        {
            return null;
        }

        try
        {
            return await client.GetFromJsonAsync<ExternalUserResponse>($"/api/users/{userId}", cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task<SideEffectResult> DeletePostAsync(string postId, string reason, CancellationToken cancellationToken)
    {
        return TryPostAsync("post", $"/api/posts/{postId}/moderation-delete", new { reason }, cancellationToken);
    }

    public Task<SideEffectResult> SetUserBlockedAsync(UserBlock block, CancellationToken cancellationToken)
    {
        return TryPostAsync("auth", $"/api/users/{block.BlockedUserId}/status", new { status = "BLOCKED", reason = block.Reason, expiresAtUtc = block.ExpiresAtUtc }, cancellationToken);
    }

    public Task<SideEffectResult> SetUserActiveAsync(string userId, CancellationToken cancellationToken)
    {
        return TryPostAsync("auth", $"/api/users/{userId}/status", new { status = "ACTIVE", reason = (string?)null, expiresAtUtc = (DateTimeOffset?)null }, cancellationToken);
    }

    public Task<SideEffectResult> NotifyPostDeletedAsync(string postId, string reason, CancellationToken cancellationToken)
    {
        return Task.FromResult(SideEffectResult.Success("notifications", "/api/notification-events"));
    }

    public Task<SideEffectResult> NotifyUserBlockedAsync(UserBlock block, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(block.BlockedUserId, out var recipientUserId))
        {
            _logger.LogWarning("Notification was skipped because blocked user id {UserId} is not a valid GUID.", block.BlockedUserId);
            return Task.FromResult(SideEffectResult.Failed("notifications", "/api/notification-events", "Invalid recipient user id."));
        }

        return TryPostAsync("notifications", "/api/notification-events", new
        {
            recipientUserId,
            type = 7,
            title = "Аккаунт ограничен",
            message = $"Ваш аккаунт ограничен по причине: {block.Reason}",
            sourceService = "ModerationService",
            sourceEntityId = block.Id
        }, cancellationToken);
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
