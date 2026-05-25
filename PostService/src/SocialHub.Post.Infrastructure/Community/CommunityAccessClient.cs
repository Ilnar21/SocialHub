using System.Net;
using System.Net.Http.Json;
using SocialHub.Post.Application.Abstractions;

namespace SocialHub.Post.Infrastructure.Community;

public sealed class CommunityAccessClient : ICommunityAccessClient
{
    private readonly HttpClient _httpClient;
    private readonly CommunityAccessOptions _options;

    public CommunityAccessClient(HttpClient httpClient, CommunityAccessOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<bool> IsMemberAsync(Guid userId, Guid communityId, CancellationToken cancellationToken)
    {
        if (_options.SkipMembershipCheck)
        {
            return true;
        }

        var response = await _httpClient.GetAsync(
            $"/communities/{communityId}/members/{userId}",
            cancellationToken);

        return response.StatusCode switch
        {
            HttpStatusCode.OK => true,
            HttpStatusCode.NotFound => false,
            HttpStatusCode.Forbidden => false,
            _ => response.IsSuccessStatusCode
        };
    }

    public async Task<bool> IsOwnerAsync(Guid userId, Guid communityId, CancellationToken cancellationToken)
    {
        if (_options.SkipMembershipCheck)
        {
            return true;
        }

        var response = await _httpClient.GetAsync(
            $"/communities/{communityId}/owners/{userId}",
            cancellationToken);

        return response.StatusCode switch
        {
            HttpStatusCode.OK => true,
            HttpStatusCode.NotFound => false,
            HttpStatusCode.Forbidden => false,
            _ => response.IsSuccessStatusCode
        };
    }

    public async Task<bool> CanViewPostsAsync(Guid? userId, Guid communityId, CancellationToken cancellationToken)
    {
        if (_options.SkipMembershipCheck)
        {
            return true;
        }

        var path = userId.HasValue
            ? $"/communities/{communityId}/post-visibility?userId={userId.Value}"
            : $"/communities/{communityId}/post-visibility";
        var response = await _httpClient.GetAsync(path, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var payload = await response.Content.ReadFromJsonAsync<PostVisibilityResponse>(cancellationToken: cancellationToken);
        return payload?.CanViewPosts == true;
    }

    private sealed record PostVisibilityResponse(bool CanViewPosts);
}
