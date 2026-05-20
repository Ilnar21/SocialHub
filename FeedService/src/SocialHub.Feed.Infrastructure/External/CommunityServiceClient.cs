using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SocialHub.Feed.Application.Abstractions;

namespace SocialHub.Feed.Infrastructure.External;

/// <summary>
/// HTTP-реализация клиента Community Service.
/// Контракт: GET {baseUrl}/internal/users/{userId}/community-ids → JSON-массив Guid.
///
/// Класс изолирован в Infrastructure: при появлении gRPC/Kafka достаточно
/// реализовать ICommunityServiceClient заново, бизнес-логика не меняется.
/// </summary>
public sealed class CommunityServiceClient : ICommunityServiceClient
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger<CommunityServiceClient> _logger;

    public CommunityServiceClient(HttpClient http, ILogger<CommunityServiceClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Guid>> GetUserCommunityIdsAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            using var response = await _http.GetAsync(
                $"internal/users/{userId:N}/community-ids", ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Community Service responded with {Status} for user {UserId}",
                    (int)response.StatusCode, userId);
                return Array.Empty<Guid>();
            }

            var ids = await response.Content.ReadFromJsonAsync<List<Guid>>(JsonOpts, ct);
            return (IReadOnlyList<Guid>?)ids ?? Array.Empty<Guid>();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "Failed to fetch communities for user {UserId}", userId);
            return Array.Empty<Guid>();
        }
    }
}
