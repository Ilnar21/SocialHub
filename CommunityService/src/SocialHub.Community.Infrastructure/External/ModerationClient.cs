using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocialHub.Community.Application.Abstractions;

namespace SocialHub.Community.Infrastructure.External;

public sealed class ModerationClient : IModerationClient
{
    private readonly HttpClient _httpClient;
    private readonly ExternalServiceOptions _options;
    private readonly ILogger<ModerationClient> _logger;

    public ModerationClient(HttpClient httpClient, IOptions<ExternalServiceOptions> options, ILogger<ModerationClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> DeleteCommunityReportsAsync(Guid communityId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ModerationBaseUrl))
        {
            _logger.LogInformation("Moderation Service is not configured. Report cleanup for community {CommunityId} was skipped by stub.", communityId);
            return true;
        }

        try
        {
            var response = await _httpClient.DeleteAsync($"/internal/reports/COMMUNITY/{communityId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            _logger.LogWarning("Moderation Service returned {StatusCode} while deleting community reports for {CommunityId}.", response.StatusCode, communityId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Moderation Service is unavailable. Community {CommunityId} cannot be deleted safely.", communityId);
            return false;
        }
    }
}
