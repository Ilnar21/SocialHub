using System.ComponentModel.DataAnnotations;

namespace SocialHub.Community.Application.Models.JoinRequests;

public sealed record RejectJoinRequestRequest([MaxLength(500)] string? Comment);
