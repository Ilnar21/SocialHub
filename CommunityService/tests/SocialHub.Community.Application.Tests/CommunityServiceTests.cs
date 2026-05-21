using SocialHub.Community.Application.Abstractions;
using SocialHub.Community.Application.Exceptions;
using SocialHub.Community.Application.Models.Communities;
using SocialHub.Community.Application.Models.External;
using SocialHub.Community.Application.Models.SuggestedPosts;
using SocialHub.Community.Domain.Entities;
using SocialHub.Community.Domain.Enums;
using CommunityAppService = SocialHub.Community.Application.Services.CommunityService;
using CommunityEntity = SocialHub.Community.Domain.Entities.Community;

namespace SocialHub.Community.Application.Tests;

public sealed class CommunityServiceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OwnerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AdminId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task CreateCommunityAsync_AddsCurrentUserAsOwner()
    {
        var repository = new FakeCommunityRepository();
        var service = CreateService(repository);

        var response = await service.CreateCommunityAsync(
            new CreateCommunityRequest("Architecture Club", "Course project", CommunityType.Open),
            CancellationToken.None);

        Assert.Equal(UserId, response.CreatedByUserId);
        Assert.Equal("Owner", response.CurrentUserMembership?.Role.ToString());
        Assert.Single(repository.Communities);
        Assert.Single(repository.AuditLogs);
    }

    [Fact]
    public async Task JoinCommunityAsync_RejectsWhenUserHasThirtyCommunities()
    {
        var repository = new FakeCommunityRepository();
        for (var index = 0; index < 30; index++)
        {
            repository.AddSeedCommunity($"Joined {index}", OwnerId, UserId);
        }

        var target = repository.AddSeedCommunity("Target", OwnerId);
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            service.JoinCommunityAsync(target.Id, CancellationToken.None));

        Assert.Equal(409, exception.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUserCommunitiesAsync_ReturnsOnlyJoinedCommunities()
    {
        var repository = new FakeCommunityRepository();
        repository.AddSeedCommunity("Joined", OwnerId, UserId);
        repository.AddSeedCommunity("Not Joined", OwnerId);
        var service = CreateService(repository);

        var response = await service.GetCurrentUserCommunitiesAsync(CancellationToken.None);

        Assert.Single(response);
        Assert.Equal("Joined", response[0].Name);
    }

    [Fact]
    public async Task LeaveCommunityAsync_RejectsOwner()
    {
        var repository = new FakeCommunityRepository();
        var community = repository.AddSeedCommunity("Owned", UserId);
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            service.LeaveCommunityAsync(community.Id, CancellationToken.None));

        Assert.Equal(409, exception.StatusCode);
    }

    [Fact]
    public async Task SubmitSuggestedPostAsync_NotifiesCommunityAdmins()
    {
        var repository = new FakeCommunityRepository();
        var notificationClient = new FakeNotificationClient();
        var community = repository.AddSeedCommunity("Moderated", OwnerId, UserId);
        var admin = community.AddMember(AdminId, DateTime.UtcNow);
        admin.ChangeRole(CommunityMemberRole.Admin);
        var service = CreateService(repository, notificationClient);

        var response = await service.SubmitSuggestedPostAsync(
            community.Id,
            new SubmitSuggestedPostRequest("Draft", "Text"),
            CancellationToken.None);

        Assert.Equal(SuggestedPostStatus.Pending, response.Status);
        Assert.Equal(2, notificationClient.Requests.Count);
        Assert.All(notificationClient.Requests, request => Assert.Equal("SuggestedPostCreated", request.Type));
    }

    private static CommunityAppService CreateService(
        FakeCommunityRepository repository,
        FakeNotificationClient? notificationClient = null)
    {
        return new CommunityAppService(
            repository,
            new FakeCurrentUserContext(UserId),
            notificationClient ?? new FakeNotificationClient(),
            new FakePostServiceClient());
    }

    private sealed class FakeCurrentUserContext : ICurrentUserContext
    {
        public FakeCurrentUserContext(Guid userId)
        {
            UserId = userId;
        }

        public Guid UserId { get; }
        public string? PlatformRole => "User";
    }

    private sealed class FakeNotificationClient : INotificationClient
    {
        public List<InternalNotificationRequest> Requests { get; } = [];

        public Task<bool> NotifyAsync(InternalNotificationRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(true);
        }
    }

    private sealed class FakePostServiceClient : IPostServiceClient
    {
        public Task<PostPublicationResult> PublishApprovedSuggestedPostAsync(
            PublishSuggestedPostRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(PostPublicationResult.Success(Guid.NewGuid()));
        }
    }

    private sealed class FakeCommunityRepository : ICommunityRepository
    {
        public List<CommunityEntity> Communities { get; } = [];
        public List<CommunityAuditLog> AuditLogs { get; } = [];
        public List<SuggestedPost> SuggestedPosts { get; } = [];

        public CommunityEntity AddSeedCommunity(string name, Guid ownerId, Guid? memberId = null)
        {
            var community = new CommunityEntity(name, "Description", CommunityType.Open, ownerId, DateTime.UtcNow);
            community.AddOwner(ownerId, DateTime.UtcNow);
            if (memberId.HasValue)
            {
                community.AddMember(memberId.Value, DateTime.UtcNow);
            }

            Communities.Add(community);
            return community;
        }

        public Task<List<CommunityEntity>> GetCommunitiesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Communities.ToList());
        }

        public Task<List<CommunityEntity>> GetCommunitiesByUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            var communities = Communities
                .Where(x => x.Members.Any(member => member.UserId == userId))
                .ToList();

            return Task.FromResult(communities);
        }

        public Task<CommunityEntity?> GetCommunityAsync(Guid communityId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Communities.FirstOrDefault(x => x.Id == communityId));
        }

        public Task<bool> CommunityNameExistsAsync(string normalizedName, CancellationToken cancellationToken)
        {
            return Task.FromResult(Communities.Any(x => x.NormalizedName == normalizedName));
        }

        public Task<CommunityMember?> GetMemberAsync(Guid communityId, Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Communities
                .FirstOrDefault(x => x.Id == communityId)?
                .Members
                .FirstOrDefault(x => x.UserId == userId));
        }

        public Task<bool> IsMemberAsync(Guid communityId, Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Communities
                .FirstOrDefault(x => x.Id == communityId)?
                .Members
                .Any(x => x.UserId == userId) == true);
        }

        public Task<List<CommunityMember>> GetMembersAsync(Guid communityId, CancellationToken cancellationToken)
        {
            var members = Communities
                .FirstOrDefault(x => x.Id == communityId)?
                .Members
                .ToList() ?? [];

            return Task.FromResult(members);
        }

        public Task<List<Guid>> GetCommunityIdsByUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            var ids = Communities
                .Where(x => x.Members.Any(member => member.UserId == userId))
                .Select(x => x.Id)
                .ToList();

            return Task.FromResult(ids);
        }

        public Task<int> CountMembershipsAsync(Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Communities.Count(x => x.Members.Any(member => member.UserId == userId)));
        }

        public Task<SuggestedPost?> GetSuggestedPostAsync(Guid communityId, Guid suggestedPostId, CancellationToken cancellationToken)
        {
            return Task.FromResult(SuggestedPosts.FirstOrDefault(x => x.CommunityId == communityId && x.Id == suggestedPostId));
        }

        public Task<List<SuggestedPost>> GetSuggestedPostsAsync(Guid communityId, SuggestedPostStatus? status, CancellationToken cancellationToken)
        {
            var query = SuggestedPosts.Where(x => x.CommunityId == communityId);
            if (status.HasValue)
            {
                query = query.Where(x => x.Status == status);
            }

            return Task.FromResult(query.ToList());
        }

        public Task AddCommunityAsync(CommunityEntity community, CancellationToken cancellationToken)
        {
            Communities.Add(community);
            return Task.CompletedTask;
        }

        public Task AddSuggestedPostAsync(SuggestedPost suggestedPost, CancellationToken cancellationToken)
        {
            SuggestedPosts.Add(suggestedPost);
            return Task.CompletedTask;
        }

        public Task AddAuditLogAsync(CommunityAuditLog auditLog, CancellationToken cancellationToken)
        {
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        public void RemoveMember(CommunityMember member)
        {
            // Domain collection removal is covered by EF in production; tests only assert service decisions.
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
