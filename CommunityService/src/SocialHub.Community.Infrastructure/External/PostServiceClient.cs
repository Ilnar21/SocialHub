using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocialHub.Community.Application.Abstractions;
using SocialHub.Community.Application.Models.External;

namespace SocialHub.Community.Infrastructure.External;

public sealed class PostServiceClient : IPostServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ExternalServiceOptions _options;
    private readonly ILogger<PostServiceClient> _logger;

    public PostServiceClient(HttpClient httpClient, IOptions<ExternalServiceOptions> options, ILogger<PostServiceClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PostPublicationResult> PublishApprovedSuggestedPostAsync(PublishSuggestedPostRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.PostBaseUrl))
        {
            var stubPostId = Guid.NewGuid();
            _logger.LogInformation("Post Service is not configured. Stub published suggested post {SuggestedPostId} as {PostId}.", request.SuggestedPostId, stubPostId);
            return PostPublicationResult.Success(stubPostId);
        }

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/posts/from-suggested", request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Post Service returned {StatusCode} for suggested post {SuggestedPostId}.", response.StatusCode, request.SuggestedPostId);
                return PostPublicationResult.Deferred("Post Service did not accept publication; retry is required.");
            }

            var result = await response.Content.ReadFromJsonAsync<PostPublicationResult>(cancellationToken: cancellationToken);
            return result ?? PostPublicationResult.Deferred("Post Service returned an empty response.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Post Service is unavailable. Suggested post approval was saved for later retry.");
            return PostPublicationResult.Deferred("Post Service is unavailable; publication should be retried later.");
        }
    }

    public async Task<bool> DeletePostsByCommunityAsync(Guid communityId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.PostBaseUrl))
        {
            _logger.LogInformation("Post Service is not configured. Post cleanup for community {CommunityId} was skipped by stub.", communityId);
            return true;
        }

        try
        {
            var response = await _httpClient.DeleteAsync($"/internal/posts/by-community/{communityId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            _logger.LogWarning("Post Service returned {StatusCode} while deleting posts for community {CommunityId}.", response.StatusCode, communityId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Post Service is unavailable. Community {CommunityId} cannot be deleted safely.", communityId);
            return false;
        }
    }
}
