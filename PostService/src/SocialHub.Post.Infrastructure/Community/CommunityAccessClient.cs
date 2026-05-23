using System.Net;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using SocialHub.Community.Contracts;
using SocialHub.Post.Application.Abstractions;

namespace SocialHub.Post.Infrastructure.Community;

public sealed class CommunityAccessClient : ICommunityAccessClient
{
    private readonly HttpClient _httpClient;
    private readonly CommunityInternal.CommunityInternalClient _grpcClient;
    private readonly CommunityAccessOptions _options;
    private readonly ILogger<CommunityAccessClient> _logger;

    public CommunityAccessClient(
        HttpClient httpClient,
        CommunityInternal.CommunityInternalClient grpcClient,
        CommunityAccessOptions options,
        ILogger<CommunityAccessClient> logger)
    {
        _httpClient = httpClient;
        _grpcClient = grpcClient;
        _options = options;
        _logger = logger;
    }

    public async Task<bool> IsMemberAsync(Guid userId, Guid communityId, CancellationToken cancellationToken)
    {
        if (_options.SkipMembershipCheck)
        {
            return true;
        }

        try
        {
            var grpcResponse = await _grpcClient.CheckMembershipAsync(
                new CheckMembershipRequest
                {
                    UserId = userId.ToString("D"),
                    CommunityId = communityId.ToString("D")
                },
                BuildGrpcMetadata(),
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Community gRPC membership check returned {IsMember} for user {UserId} in community {CommunityId}",
                grpcResponse.IsMember, userId, communityId);

            return grpcResponse.IsMember;
        }
        catch (Exception ex) when (ex is RpcException or HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Community gRPC membership check failed for user {UserId} in community {CommunityId}. Falling back to REST.",
                userId, communityId);
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
