namespace SocialHub.Feed.Application.Abstractions;

/// <summary>
/// Current authenticated user context resolved in the API layer from JWT claims.
/// </summary>
public interface ICurrentUserContext
{
    Guid? UserId { get; }
    bool IsAuthenticated { get; }
}
