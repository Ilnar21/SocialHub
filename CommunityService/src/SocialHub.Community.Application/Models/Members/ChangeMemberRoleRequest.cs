using SocialHub.Community.Domain.Enums;

namespace SocialHub.Community.Application.Models.Members;

public sealed record ChangeMemberRoleRequest(CommunityMemberRole Role);
