using Grpc.Core;
using Microsoft.Extensions.Options;
using SocialHub.Community.Api.Security;
using SocialHub.Community.Application.Abstractions;
using SocialHub.Community.Contracts;

namespace SocialHub.Community.Api.Grpc;

public sealed class InternalCommunityGrpcService : CommunityInternal.CommunityInternalBase
{
    private const string InternalTokenHeader = "x-internal-token";

    private readonly ICommunityService _communityService;
    private readonly InternalAuthOptions _authOptions;

    public InternalCommunityGrpcService(
        ICommunityService communityService,
        IOptions<InternalAuthOptions> authOptions)
    {
        _communityService = communityService;
        _authOptions = authOptions.Value;
    }

    public override async Task<GetUserCommunityIdsReply> GetUserCommunityIds(
        GetUserCommunityIdsRequest request,
        ServerCallContext context)
    {
        EnsureInternalAccess(context);
        var userId = ParseGuid(request.UserId, nameof(request.UserId));
        var ids = await _communityService.GetCommunityIdsByUserAsync(userId, context.CancellationToken);

        var reply = new GetUserCommunityIdsReply();
        reply.CommunityIds.AddRange(ids.Select(id => id.ToString("D")));
        return reply;
    }

    public override async Task<CheckMembershipReply> CheckMembership(
        CheckMembershipRequest request,
        ServerCallContext context)
    {
        EnsureInternalAccess(context);
        var communityId = ParseGuid(request.CommunityId, nameof(request.CommunityId));
        var userId = ParseGuid(request.UserId, nameof(request.UserId));
        var isMember = await _communityService.IsMemberAsync(communityId, userId, context.CancellationToken);
        return new CheckMembershipReply { IsMember = isMember };
    }

    private void EnsureInternalAccess(ServerCallContext context)
    {
        if (!_authOptions.RequireInternalToken)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_authOptions.Token))
        {
            throw new RpcException(new Status(StatusCode.Unavailable, "Internal token is not configured."));
        }

        var token = context.RequestHeaders
            .FirstOrDefault(header => string.Equals(header.Key, InternalTokenHeader, StringComparison.OrdinalIgnoreCase))
            ?.Value;

        if (!string.Equals(token, _authOptions.Token, StringComparison.Ordinal))
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid internal token."));
        }
    }

    private static Guid ParseGuid(string value, string fieldName)
    {
        if (Guid.TryParse(value, out var guid))
        {
            return guid;
        }

        throw new RpcException(new Status(StatusCode.InvalidArgument, $"{fieldName} must be a valid UUID."));
    }
}
