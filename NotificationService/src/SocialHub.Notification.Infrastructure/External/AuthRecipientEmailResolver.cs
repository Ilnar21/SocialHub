using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocialHub.Notification.Application.Abstractions;

namespace SocialHub.Notification.Infrastructure.External;

public sealed class AuthRecipientEmailResolver : IRecipientEmailResolver
{
    private readonly HttpClient _httpClient;
    private readonly ExternalServiceOptions _options;
    private readonly ILogger<AuthRecipientEmailResolver> _logger;

    public AuthRecipientEmailResolver(
        HttpClient httpClient,
        IOptions<ExternalServiceOptions> options,
        ILogger<AuthRecipientEmailResolver> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string?> ResolveEmailAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.AuthBaseUrl))
        {
            return null;
        }

        try
        {
            var user = await _httpClient.GetFromJsonAsync<AuthUserResponse>($"/api/users/{userId}", cancellationToken);
            return string.IsNullOrWhiteSpace(user?.Email) ? null : user.Email;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not resolve email for user {UserId}. Email notification will be skipped.", userId);
            return null;
        }
    }

    private sealed record AuthUserResponse(Guid Id, string Username, string Email);
}
