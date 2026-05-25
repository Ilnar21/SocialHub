using System.ComponentModel.DataAnnotations;
using SocialHub.Community.Domain.Constants;

namespace SocialHub.Community.Application.Models.Communities;

public sealed record UpdateCommunityRequest(
    [MaxLength(CommunityLimits.DescriptionMaxLength)] string? Description);
