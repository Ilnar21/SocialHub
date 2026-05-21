using System.ComponentModel.DataAnnotations;

namespace SocialHub.Community.Application.Models.SuggestedPosts;

public sealed record RejectSuggestedPostRequest([MaxLength(500)] string? Comment);
