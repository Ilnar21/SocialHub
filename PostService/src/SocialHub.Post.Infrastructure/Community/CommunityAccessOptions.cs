namespace SocialHub.Post.Infrastructure.Community;

public sealed class CommunityAccessOptions
{
    public string BaseUrl { get; set; } = "http://community-service:8080";
    public string GrpcUrl { get; set; } = "http://community-service:8081";
    public bool SkipMembershipCheck { get; set; } = true;
    public string? InternalToken { get; set; }
}
