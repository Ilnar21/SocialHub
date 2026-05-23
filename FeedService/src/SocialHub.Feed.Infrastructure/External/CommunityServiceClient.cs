using System.Net.Http.Json;
using System.Text.Json;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocialHub.Community.Contracts;
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
    private readonly CommunityInternal.CommunityInternalClient _grpc;
    private readonly ExternalServiceOptions _options;
    private readonly ILogger<CommunityServiceClient> _logger;

    public CommunityServiceClient(
        HttpClient http,
        CommunityInternal.CommunityInternalClient grpc,
        IOptions<ExternalServiceOptions> options,
        ILogger<CommunityServiceClient> logger)
    {
        _http = http;
        _grpc = grpc;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Guid>> GetUserCommunityIdsAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            var response = await _grpc.GetUserCommunityIdsAsync(
                new GetUserCommunityIdsRequest { UserId = userId.ToString("D") },
                BuildGrpcMetadata(),
                cancellationToken: ct);

            var ids = response.CommunityIds
                .Select(id => Guid.TryParse(id, out var parsed) ? parsed : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .ToArray();

            _logger.LogInformation(
                "Community gRPC returned {Count} community ids for user {UserId}",
                ids.Length, userId);

            return ids;
        }
        catch (Exception ex) when (ex is RpcException or HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Community gRPC failed for user {UserId}. Falling back to REST.",
                userId);

            return await GetUserCommunityIdsByRestAsync(userId, ct);
        }
    }

    private async Task<IReadOnlyList<Guid>> GetUserCommunityIdsByRestAsync(Guid userId, CancellationToken ct)
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

    private Metadata BuildGrpcMetadata()
    {
        var metadata = new Metadata();
        if (!string.IsNullOrWhiteSpace(_options.InternalToken))
        {
            metadata.Add("x-internal-token", _options.InternalToken);
        }

        return metadata;
    }
}
