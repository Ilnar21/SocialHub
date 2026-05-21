using SocialHub.Post.Application.Abstractions;

namespace SocialHub.Post.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
