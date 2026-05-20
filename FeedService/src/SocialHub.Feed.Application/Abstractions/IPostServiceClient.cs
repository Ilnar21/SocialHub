using SocialHub.Feed.Application.Models.External;

namespace SocialHub.Feed.Application.Abstractions;

/// <summary>
/// Клиент Post Service. Возвращает «снимки» постов по списку сообществ
/// — этого набора данных достаточно для ранжирования.
/// </summary>
public interface IPostServiceClient
{
    Task<IReadOnlyList<PostSnapshot>> GetPostsByCommunitiesAsync(
        IReadOnlyCollection<Guid> communityIds,
        int limit,
        CancellationToken ct = default);
}
