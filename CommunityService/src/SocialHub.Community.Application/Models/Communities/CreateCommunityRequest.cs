using System.ComponentModel.DataAnnotations;
using SocialHub.Community.Domain.Constants;
using SocialHub.Community.Domain.Enums;

namespace SocialHub.Community.Application.Models.Communities;

public sealed record CreateCommunityRequest(
    [Required, MaxLength(CommunityLimits.NameMaxLength)] string Name,
    [MaxLength(CommunityLimits.DescriptionMaxLength)] string Description,
    CommunityType Type = CommunityType.Open);
