using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SocialHub.Feed.Application.Abstractions;
using SocialHub.Feed.Application.Models.External;

namespace SocialHub.Feed.Infrastructure.External;

/// <summary>
/// HTTP-реализация клиента Post Service.
/// Контракт: POST {baseUrl}/internal/posts/by-communities
///   { "communityIds": [Guid, ...], "limit": int } → массив PostSnapshot.
/// </summary>
public sealed class PostServiceClient : IPostServiceClient
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger<PostServiceClient> _logger;

    public PostServiceClient(HttpClient http, ILogger<PostServiceClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PostSnapshot>> GetPostsByCommunitiesAsync(
        IReadOnlyCollection<Guid> communityIds,
        int limit,
        CancellationToken ct = default)
    {
        if (communityIds.Count == 0)
        {
            return Array.Empty<PostSnapshot>();
        }

        try
        {
            using var response = await _http.PostAsJsonAsync(
                "internal/posts/by-communities",
                new PostsByCommunitiesRequest(communityIds, limit),
                JsonOpts,
                ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Post Service responded with {Status} for {Count} communities",
                    (int)response.StatusCode, communityIds.Count);
                return Array.Empty<PostSnapshot>();
            }

            var posts = await response.Content.ReadFromJsonAsync<List<PostSnapshot>>(JsonOpts, ct);
            return (IReadOnlyList<PostSnapshot>?)posts ?? Array.Empty<PostSnapshot>();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "Failed to fetch posts for {Count} communities", communityIds.Count);
            return Array.Empty<PostSnapshot>();
        }
    }

    private sealed record PostsByCommunitiesRequest(
        IReadOnlyCollection<Guid> CommunityIds,
        int Limit);
}
