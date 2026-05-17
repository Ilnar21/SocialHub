using System.ComponentModel.DataAnnotations;
using SocialHub.Community.Domain.Constants;

namespace SocialHub.Community.Application.Models.SuggestedPosts;

public sealed record SubmitSuggestedPostRequest(
    [Required, MaxLength(CommunityLimits.SuggestedPostTitleMaxLength)] string Title,
    [Required, MaxLength(CommunityLimits.SuggestedPostTextMaxLength)] string Text);
