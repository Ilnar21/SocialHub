namespace SocialHub.Community.Application.Models.External;

public sealed record PostPublicationResult(bool Succeeded, Guid? PostId, string? Warning)
{
    public static PostPublicationResult Success(Guid postId) => new(true, postId, null);
    public static PostPublicationResult Deferred(string warning) => new(false, null, warning);
}
