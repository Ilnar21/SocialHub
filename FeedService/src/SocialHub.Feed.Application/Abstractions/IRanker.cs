using SocialHub.Feed.Application.Models.External;

namespace SocialHub.Feed.Application.Abstractions;

/// <summary>
/// Алгоритм ранжирования. Реализован как отдельный модуль,
/// чтобы можно было заменить формулу/добавить ML-ранжирование без переписывания FeedService.
/// </summary>
public interface IRanker
{
    double Score(PostSnapshot post, DateTimeOffset now);
}
