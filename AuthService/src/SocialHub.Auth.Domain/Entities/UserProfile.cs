namespace SocialHub.Auth.Domain.Entities;

public sealed class UserProfile
{
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
}
