using SocialHub.Community.Application.Abstractions;
using SocialHub.Community.Application.Exceptions;
using SocialHub.Community.Application.Models.Communities;
using SocialHub.Community.Application.Models.External;
using SocialHub.Community.Application.Models.JoinRequests;
using SocialHub.Community.Application.Models.Members;
using SocialHub.Community.Application.Models.SuggestedPosts;
using SocialHub.Community.Domain.Constants;
using SocialHub.Community.Domain.Entities;
using SocialHub.Community.Domain.Enums;

namespace SocialHub.Community.Application.Services;

public sealed class CommunityService : ICommunityService
{
    private readonly ICommunityRepository _repository;
    private readonly ICurrentUserContext _currentUser;
    private readonly INotificationClient _notificationClient;
    private readonly IPostServiceClient _postServiceClient;

    public CommunityService(
        ICommunityRepository repository,
        ICurrentUserContext currentUser,
        INotificationClient notificationClient,
        IPostServiceClient postServiceClient)
    {
        _repository = repository;
        _currentUser = currentUser;
        _notificationClient = notificationClient;
        _postServiceClient = postServiceClient;
    }

    public async Task<List<CommunitySummaryResponse>> GetCommunitiesAsync(CancellationToken cancellationToken)
    {
        var communities = await _repository.GetCommunitiesAsync(cancellationToken);
        return communities
            .OrderBy(c => c.Name)
            .Select(c => ToSummary(c))
            .ToList();
    }

    public async Task<List<CommunitySummaryResponse>> GetCurrentUserCommunitiesAsync(CancellationToken cancellationToken)
    {
        var communities = await _repository.GetCommunitiesByUserAsync(_currentUser.UserId, cancellationToken);
        var response = new List<CommunitySummaryResponse>();
        foreach (var community in communities.OrderBy(c => c.Name))
        {
            var currentMembership = await _repository.GetMemberAsync(community.Id, _currentUser.UserId, cancellationToken);
            response.Add(ToSummary(community, currentMembership));
        }

        return response;
    }

    public async Task<CommunityDetailsResponse> GetCommunityAsync(Guid communityId, CancellationToken cancellationToken)
    {
        var community = await GetRequiredCommunityAsync(communityId, cancellationToken);
        var currentMembership = await _repository.GetMemberAsync(communityId, _currentUser.UserId, cancellationToken);
        var pendingRequest = currentMembership is null
            ? await _repository.GetPendingJoinRequestAsync(communityId, _currentUser.UserId, cancellationToken)
            : null;
        return ToDetails(community, currentMembership, pendingRequest);
    }

    public async Task<CommunityDetailsResponse> GetCommunityByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        var normalizedUsername = NormalizeCommunityUsernameOrThrow(username).ToUpperInvariant();
        var community = await _repository.GetCommunityByUsernameAsync(normalizedUsername, cancellationToken)
            ?? throw AppException.NotFound("Сообщество не найдено.");

        var currentMembership = await _repository.GetMemberAsync(community.Id, _currentUser.UserId, cancellationToken);
        var pendingRequest = currentMembership is null
            ? await _repository.GetPendingJoinRequestAsync(community.Id, _currentUser.UserId, cancellationToken)
            : null;
        return ToDetails(community, currentMembership, pendingRequest);
    }

    public async Task<CommunityDetailsResponse> CreateCommunityAsync(CreateCommunityRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw AppException.BadRequest("Укажите название сообщества.");
        }

        var normalizedName = request.Name.Trim().ToUpperInvariant();
        if (await _repository.CommunityNameExistsAsync(normalizedName, cancellationToken))
        {
            throw AppException.Conflict("Сообщество с таким названием уже существует.");
        }

        var username = NormalizeCommunityUsernameOrThrow(request.Username);
        var normalizedUsername = username.ToUpperInvariant();
        if (await _repository.CommunityUsernameExistsAsync(normalizedUsername, cancellationToken))
        {
            throw AppException.Conflict("Юзернейм сообщества уже занят.");
        }

        var membershipCount = await _repository.CountMembershipsAsync(_currentUser.UserId, cancellationToken);
        if (membershipCount >= CommunityLimits.MaxCommunitiesPerUser)
        {
            throw AppException.Conflict("Нельзя состоять больше чем в 30 сообществах.");
        }

        var now = DateTime.UtcNow;
        var community = new Community.Domain.Entities.Community(request.Name, username, request.Description, request.Type, _currentUser.UserId, now);
        var owner = community.AddOwner(_currentUser.UserId, now);

        await _repository.AddCommunityAsync(community, cancellationToken);
        await _repository.AddAuditLogAsync(new CommunityAuditLog(community.Id, _currentUser.UserId, "COMMUNITY_CREATED", $"Community '{community.Name}' was created.", now), cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return ToDetails(community, owner, null);
    }

    public async Task<CommunityDetailsResponse> UpdateCommunityAsync(Guid communityId, UpdateCommunityRequest request, CancellationToken cancellationToken)
    {
        var community = await GetRequiredCommunityAsync(communityId, cancellationToken);
        var member = await GetRequiredMemberAsync(communityId, _currentUser.UserId, cancellationToken);
        if (member.Role != CommunityMemberRole.Owner)
        {
            throw AppException.Forbidden("Только владелец сообщества может менять описание.");
        }

        var now = DateTime.UtcNow;
        community.UpdateDescription(request.Description, now);
        await _repository.AddAuditLogAsync(new CommunityAuditLog(communityId, _currentUser.UserId, "COMMUNITY_DESCRIPTION_UPDATED", "Community description was updated.", now), cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return ToDetails(community, member, null);
    }

    public async Task<MemberResponse> JoinCommunityAsync(Guid communityId, CancellationToken cancellationToken)
    {
        var community = await GetRequiredCommunityAsync(communityId, cancellationToken);
        var existingMember = await _repository.GetMemberAsync(communityId, _currentUser.UserId, cancellationToken);
        if (existingMember is not null)
        {
            return ToMemberResponse(existingMember);
        }

        if (community.Type == CommunityType.Closed)
        {
            throw AppException.Conflict("Сообщество закрытое. Для вступления отправьте заявку владельцу.");
        }

        var membershipCount = await _repository.CountMembershipsAsync(_currentUser.UserId, cancellationToken);
        if (membershipCount >= CommunityLimits.MaxCommunitiesPerUser)
        {
            throw AppException.Conflict("Нельзя состоять больше чем в 30 сообществах.");
        }

        var now = DateTime.UtcNow;
        var member = community.AddMember(_currentUser.UserId, now);
        await _repository.AddMemberAsync(member, cancellationToken);
        await _repository.AddAuditLogAsync(new CommunityAuditLog(communityId, _currentUser.UserId, "MEMBER_JOINED", "User joined the community.", now), cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return ToMemberResponse(member);
    }

    public async Task<JoinRequestResponse> RequestToJoinCommunityAsync(Guid communityId, CancellationToken cancellationToken)
    {
        var community = await GetRequiredCommunityAsync(communityId, cancellationToken);
        if (community.Type != CommunityType.Closed)
        {
            throw AppException.BadRequest("В открытое сообщество можно вступить без заявки.");
        }

        var existingMember = await _repository.GetMemberAsync(communityId, _currentUser.UserId, cancellationToken);
        if (existingMember is not null)
        {
            throw AppException.Conflict("Вы уже состоите в этом сообществе.");
        }

        var existingRequest = await _repository.GetPendingJoinRequestAsync(communityId, _currentUser.UserId, cancellationToken);
        if (existingRequest is not null)
        {
            return ToJoinRequestResponse(existingRequest);
        }

        var membershipCount = await _repository.CountMembershipsAsync(_currentUser.UserId, cancellationToken);
        if (membershipCount >= CommunityLimits.MaxCommunitiesPerUser)
        {
            throw AppException.Conflict("Нельзя состоять больше чем в 30 сообществах.");
        }

        var now = DateTime.UtcNow;
        var request = new CommunityJoinRequest(communityId, _currentUser.UserId, now);
        await _repository.AddJoinRequestAsync(request, cancellationToken);
        await _repository.AddAuditLogAsync(new CommunityAuditLog(communityId, _currentUser.UserId, "JOIN_REQUEST_CREATED", "User requested to join the community.", now), cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        foreach (var owner in community.Members.Where(member => member.Role == CommunityMemberRole.Owner))
        {
            await _notificationClient.NotifyAsync(
                new InternalNotificationRequest(
                    owner.UserId,
                    "JoinRequestCreated",
                    "Новая заявка на вступление",
                    $"Пользователь хочет вступить в закрытое сообщество «{community.Name}».",
                    communityId,
                    request.Id),
                cancellationToken);
        }

        return ToJoinRequestResponse(request);
    }

    public async Task LeaveCommunityAsync(Guid communityId, CancellationToken cancellationToken)
    {
        await GetRequiredCommunityAsync(communityId, cancellationToken);
        var member = await GetRequiredMemberAsync(communityId, _currentUser.UserId, cancellationToken);
        if (member.Role == CommunityMemberRole.Owner)
        {
            throw AppException.Conflict("Владелец не может выйти из сообщества, пока не передаст владение.");
        }

        _repository.RemoveMember(member);
        await _repository.AddAuditLogAsync(new CommunityAuditLog(communityId, _currentUser.UserId, "MEMBER_LEFT", "User left the community.", DateTime.UtcNow), cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<MemberResponse>> GetMembersAsync(Guid communityId, CancellationToken cancellationToken)
    {
        await EnsureCurrentUserOwnsCommunityAsync(communityId, cancellationToken);
        var members = await _repository.GetMembersAsync(communityId, cancellationToken);
        return members
            .OrderBy(m => m.Role)
            .ThenBy(m => m.JoinedAtUtc)
            .Select(ToMemberResponse)
            .ToList();
    }

    public async Task<List<JoinRequestResponse>> GetJoinRequestsAsync(
        Guid communityId,
        CommunityJoinRequestStatus? status,
        CancellationToken cancellationToken)
    {
        await EnsureCurrentUserOwnsCommunityAsync(communityId, cancellationToken);
        var requests = await _repository.GetJoinRequestsAsync(communityId, status, cancellationToken);
        return requests
            .OrderByDescending(request => request.CreatedAtUtc)
            .Select(ToJoinRequestResponse)
            .ToList();
    }

    public async Task<JoinRequestResponse> ApproveJoinRequestAsync(
        Guid communityId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        await EnsureCurrentUserOwnsCommunityAsync(communityId, cancellationToken);
        var community = await GetRequiredCommunityAsync(communityId, cancellationToken);
        var request = await GetRequiredJoinRequestAsync(communityId, requestId, cancellationToken);
        if (request.Status != CommunityJoinRequestStatus.Pending)
        {
            throw AppException.Conflict("Заявка на вступление уже рассмотрена.");
        }

        var existingMember = await _repository.GetMemberAsync(communityId, request.UserId, cancellationToken);
        if (existingMember is null)
        {
            var membershipCount = await _repository.CountMembershipsAsync(request.UserId, cancellationToken);
            if (membershipCount >= CommunityLimits.MaxCommunitiesPerUser)
            {
                throw AppException.Conflict("Пользователь уже состоит в 30 сообществах.");
            }

            var member = community.AddMember(request.UserId, DateTime.UtcNow);
            await _repository.AddMemberAsync(member, cancellationToken);
        }

        var now = DateTime.UtcNow;
        request.Approve(_currentUser.UserId, now);
        await _repository.AddAuditLogAsync(new CommunityAuditLog(communityId, _currentUser.UserId, "JOIN_REQUEST_APPROVED", $"Join request '{request.Id}' was approved.", now), cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        await _notificationClient.NotifyAsync(
            new InternalNotificationRequest(
                request.UserId,
                "JoinRequestApproved",
                "Заявка одобрена",
                $"Вас приняли в сообщество «{community.Name}».",
                communityId,
                request.Id),
            cancellationToken);

        return ToJoinRequestResponse(request);
    }

    public async Task<JoinRequestResponse> RejectJoinRequestAsync(
        Guid communityId,
        Guid requestId,
        RejectJoinRequestRequest rejectRequest,
        CancellationToken cancellationToken)
    {
        await EnsureCurrentUserOwnsCommunityAsync(communityId, cancellationToken);
        var community = await GetRequiredCommunityAsync(communityId, cancellationToken);
        var request = await GetRequiredJoinRequestAsync(communityId, requestId, cancellationToken);
        if (request.Status != CommunityJoinRequestStatus.Pending)
        {
            throw AppException.Conflict("Заявка на вступление уже рассмотрена.");
        }

        var now = DateTime.UtcNow;
        request.Reject(_currentUser.UserId, rejectRequest.Comment, now);
        await _repository.AddAuditLogAsync(new CommunityAuditLog(communityId, _currentUser.UserId, "JOIN_REQUEST_REJECTED", $"Join request '{request.Id}' was rejected.", now), cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        await _notificationClient.NotifyAsync(
            new InternalNotificationRequest(
                request.UserId,
                "JoinRequestRejected",
                "Заявка отклонена",
                $"Заявку на вступление в сообщество «{community.Name}» отклонили.",
                communityId,
                request.Id),
            cancellationToken);

        return ToJoinRequestResponse(request);
    }

    public async Task<bool> IsMemberAsync(Guid communityId, Guid userId, CancellationToken cancellationToken)
    {
        return await _repository.IsMemberAsync(communityId, userId, cancellationToken);
    }

    public async Task<bool> IsOwnerAsync(Guid communityId, Guid userId, CancellationToken cancellationToken)
    {
        var member = await _repository.GetMemberAsync(communityId, userId, cancellationToken);
        return member?.Role == CommunityMemberRole.Owner;
    }

    public async Task<bool> CanViewPostsAsync(Guid communityId, Guid? userId, CancellationToken cancellationToken)
    {
        var community = await GetRequiredCommunityAsync(communityId, cancellationToken);
        if (community.Type == CommunityType.Open)
        {
            return true;
        }

        return userId.HasValue && await _repository.IsMemberAsync(communityId, userId.Value, cancellationToken);
    }

    public async Task<List<Guid>> GetCommunityIdsByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _repository.GetCommunityIdsByUserAsync(userId, cancellationToken);
    }

    public async Task RemoveMemberAsync(Guid communityId, Guid memberUserId, CancellationToken cancellationToken)
    {
        await EnsureCurrentUserCanAdminCommunityAsync(communityId, cancellationToken);

        if (memberUserId == _currentUser.UserId)
        {
            throw AppException.BadRequest("Для выхода из сообщества используйте кнопку выхода.");
        }

        var member = await GetRequiredMemberAsync(communityId, memberUserId, cancellationToken);
        if (member.Role == CommunityMemberRole.Owner)
        {
            throw AppException.Forbidden("Владельца нельзя удалить из сообщества.");
        }

        _repository.RemoveMember(member);
        await _repository.AddAuditLogAsync(new CommunityAuditLog(communityId, _currentUser.UserId, "MEMBER_REMOVED", $"User '{memberUserId}' was removed from community.", DateTime.UtcNow), cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<MemberResponse> ChangeMemberRoleAsync(Guid communityId, Guid memberUserId, CommunityMemberRole role, CancellationToken cancellationToken)
    {
        var actor = await GetRequiredMemberAsync(communityId, _currentUser.UserId, cancellationToken);
        if (actor.Role != CommunityMemberRole.Owner)
        {
            throw AppException.Forbidden("Только владелец сообщества может менять роли участников.");
        }

        if (role == CommunityMemberRole.Owner)
        {
            throw AppException.BadRequest("Передача владения через этот экран пока не поддерживается.");
        }

        var member = await GetRequiredMemberAsync(communityId, memberUserId, cancellationToken);
        if (member.Role == CommunityMemberRole.Owner)
        {
            throw AppException.Forbidden("Роль владельца нельзя изменить через этот экран.");
        }

        member.ChangeRole(role);
        await _repository.AddAuditLogAsync(new CommunityAuditLog(communityId, _currentUser.UserId, "MEMBER_ROLE_CHANGED", $"User '{memberUserId}' role changed to '{role}'.", DateTime.UtcNow), cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return ToMemberResponse(member);
    }

    public async Task<SuggestedPostResponse> SubmitSuggestedPostAsync(Guid communityId, SubmitSuggestedPostRequest request, CancellationToken cancellationToken)
    {
        await GetRequiredCommunityAsync(communityId, cancellationToken);
        await GetRequiredMemberAsync(communityId, _currentUser.UserId, cancellationToken);

        var suggestedPost = new SuggestedPost(communityId, _currentUser.UserId, request.Title, request.Text, DateTime.UtcNow);
        await _repository.AddSuggestedPostAsync(suggestedPost, cancellationToken);
        await _repository.AddAuditLogAsync(new CommunityAuditLog(communityId, _currentUser.UserId, "SUGGESTED_POST_CREATED", $"Suggested post '{suggestedPost.Title}' was created.", suggestedPost.CreatedAtUtc), cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        var administrators = await _repository.GetMembersAsync(communityId, cancellationToken);
        foreach (var admin in administrators.Where(m => m.Role == CommunityMemberRole.Owner))
        {
            await _notificationClient.NotifyAsync(
                new InternalNotificationRequest(
                    admin.UserId,
                    "SuggestedPostCreated",
                    "New suggested post",
                    $"A new post '{suggestedPost.Title}' is waiting for review.",
                    communityId,
                    suggestedPost.Id),
                cancellationToken);
        }

        return ToSuggestedPostResponse(suggestedPost);
    }

    public async Task<List<SuggestedPostResponse>> GetSuggestedPostsAsync(Guid communityId, SuggestedPostStatus? status, CancellationToken cancellationToken)
    {
        await EnsureCurrentUserOwnsCommunityAsync(communityId, cancellationToken);
        var posts = await _repository.GetSuggestedPostsAsync(communityId, status, cancellationToken);
        return posts
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(ToSuggestedPostResponse)
            .ToList();
    }

    public async Task<SuggestedPostResponse> ApproveSuggestedPostAsync(Guid communityId, Guid suggestedPostId, CancellationToken cancellationToken)
    {
        await EnsureCurrentUserOwnsCommunityAsync(communityId, cancellationToken);
        var suggestedPost = await GetRequiredSuggestedPostAsync(communityId, suggestedPostId, cancellationToken);

        PostPublicationResult publicationResult;
        try
        {
            publicationResult = await _postServiceClient.PublishApprovedSuggestedPostAsync(
                new PublishSuggestedPostRequest(
                    communityId,
                    suggestedPost.AuthorUserId,
                    suggestedPost.Id,
                    suggestedPost.Title,
                    suggestedPost.Text),
                cancellationToken);
        }
        catch
        {
            publicationResult = PostPublicationResult.Deferred("Post Service is unavailable; publication should be retried later.");
        }

        suggestedPost.Approve(
            _currentUser.UserId,
            publicationResult.PostId,
            publicationResult.Warning,
            DateTime.UtcNow);

        await _repository.AddAuditLogAsync(new CommunityAuditLog(communityId, _currentUser.UserId, "SUGGESTED_POST_APPROVED", $"Suggested post '{suggestedPost.Id}' was approved.", DateTime.UtcNow), cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        await _notificationClient.NotifyAsync(
            new InternalNotificationRequest(
                suggestedPost.AuthorUserId,
                "SuggestedPostApproved",
                "Your suggested post was approved",
                $"Your post '{suggestedPost.Title}' was approved.",
                communityId,
                suggestedPost.Id),
            cancellationToken);

        return ToSuggestedPostResponse(suggestedPost);
    }

    public async Task<SuggestedPostResponse> RejectSuggestedPostAsync(Guid communityId, Guid suggestedPostId, RejectSuggestedPostRequest request, CancellationToken cancellationToken)
    {
        await EnsureCurrentUserOwnsCommunityAsync(communityId, cancellationToken);
        var suggestedPost = await GetRequiredSuggestedPostAsync(communityId, suggestedPostId, cancellationToken);

        suggestedPost.Reject(_currentUser.UserId, request.Comment, DateTime.UtcNow);
        await _repository.AddAuditLogAsync(new CommunityAuditLog(communityId, _currentUser.UserId, "SUGGESTED_POST_REJECTED", $"Suggested post '{suggestedPost.Id}' was rejected.", DateTime.UtcNow), cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        await _notificationClient.NotifyAsync(
            new InternalNotificationRequest(
                suggestedPost.AuthorUserId,
                "SuggestedPostRejected",
                "Your suggested post was rejected",
                $"Your post '{suggestedPost.Title}' was rejected.",
                communityId,
                suggestedPost.Id),
            cancellationToken);

        return ToSuggestedPostResponse(suggestedPost);
    }

    private async Task<Community.Domain.Entities.Community> GetRequiredCommunityAsync(Guid communityId, CancellationToken cancellationToken)
    {
        return await _repository.GetCommunityAsync(communityId, cancellationToken)
            ?? throw AppException.NotFound("Сообщество не найдено.");
    }

    private async Task<CommunityMember> GetRequiredMemberAsync(Guid communityId, Guid userId, CancellationToken cancellationToken)
    {
        return await _repository.GetMemberAsync(communityId, userId, cancellationToken)
            ?? throw AppException.NotFound("Участник сообщества не найден.");
    }

    private async Task<SuggestedPost> GetRequiredSuggestedPostAsync(Guid communityId, Guid suggestedPostId, CancellationToken cancellationToken)
    {
        return await _repository.GetSuggestedPostAsync(communityId, suggestedPostId, cancellationToken)
            ?? throw AppException.NotFound("Предложенный пост не найден.");
    }

    private async Task<CommunityJoinRequest> GetRequiredJoinRequestAsync(Guid communityId, Guid requestId, CancellationToken cancellationToken)
    {
        return await _repository.GetJoinRequestAsync(communityId, requestId, cancellationToken)
            ?? throw AppException.NotFound("Заявка на вступление не найдена.");
    }

    private async Task EnsureCurrentUserCanAdminCommunityAsync(Guid communityId, CancellationToken cancellationToken)
    {
        await GetRequiredCommunityAsync(communityId, cancellationToken);
        var member = await GetRequiredMemberAsync(communityId, _currentUser.UserId, cancellationToken);
        if (member.Role is not (CommunityMemberRole.Owner or CommunityMemberRole.Admin))
        {
            throw AppException.Forbidden("Недостаточно прав для управления сообществом.");
        }
    }

    private async Task EnsureCurrentUserOwnsCommunityAsync(Guid communityId, CancellationToken cancellationToken)
    {
        await GetRequiredCommunityAsync(communityId, cancellationToken);
        var member = await GetRequiredMemberAsync(communityId, _currentUser.UserId, cancellationToken);
        if (member.Role != CommunityMemberRole.Owner)
        {
            throw AppException.Forbidden("Только владелец сообщества может управлять этим разделом.");
        }
    }

    private CommunityDetailsResponse ToDetails(
        Community.Domain.Entities.Community community,
        CommunityMember? currentMembership,
        CommunityJoinRequest? currentJoinRequest)
    {
        return new CommunityDetailsResponse(
            community.Id,
            community.Name,
            community.Username,
            community.Description,
            community.Type,
            community.CreatedByUserId,
            community.CreatedAtUtc,
            community.Members.Count,
            currentMembership is null ? null : ToMemberResponse(currentMembership),
            currentJoinRequest is null ? null : ToJoinRequestResponse(currentJoinRequest));
    }

    private static CommunitySummaryResponse ToSummary(
        Community.Domain.Entities.Community community,
        CommunityMember? currentMembership = null)
    {
        return new CommunitySummaryResponse(
            community.Id,
            community.Name,
            community.Username,
            community.Description,
            community.Type,
            community.CreatedAtUtc,
            community.Members.Count,
            currentMembership?.Role);
    }

    private static string NormalizeCommunityUsernameOrThrow(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw AppException.BadRequest("Укажите username сообщества.");
        }

        try
        {
            return Community.Domain.Entities.Community.NormalizeUsername(username);
        }
        catch (ArgumentException)
        {
            throw AppException.BadRequest("Юзернейм сообщества должен быть от 3 до 64 символов: латинские буквы, цифры, точка, дефис или нижнее подчеркивание. Первый символ должен быть буквой или цифрой.");
        }
    }

    private static MemberResponse ToMemberResponse(CommunityMember member)
    {
        return new MemberResponse(member.Id, member.CommunityId, member.UserId, member.Role, member.JoinedAtUtc);
    }

    private static JoinRequestResponse ToJoinRequestResponse(CommunityJoinRequest request)
    {
        return new JoinRequestResponse(
            request.Id,
            request.CommunityId,
            request.UserId,
            request.Status,
            request.CreatedAtUtc,
            request.ReviewedByUserId,
            request.ReviewedAtUtc,
            request.ReviewComment);
    }

    private static SuggestedPostResponse ToSuggestedPostResponse(SuggestedPost post)
    {
        return new SuggestedPostResponse(
            post.Id,
            post.CommunityId,
            post.AuthorUserId,
            post.Title,
            post.Text,
            post.Status,
            post.ReviewedByUserId,
            post.ReviewedAtUtc,
            post.ReviewComment,
            post.PublishedPostId,
            post.PublicationWarning,
            post.CreatedAtUtc);
    }
}
