using SocialHub.Feed.Application.Abstractions;
using SocialHub.Feed.Application.Models.External;

namespace SocialHub.Feed.Application.Ranking;

/// <summary>
/// Базовая формула ранжирования по популярности:
///     Score = Likes * 2 + Comments * 1.5 + Freshness
/// где Freshness плавно убывает со временем и обнуляется на горизонте недель.
/// </summary>
public sealed class PopularityRanker : IRanker
{
    private const double LikeWeight = 2.0;
    private const double CommentWeight = 1.5;

    // Свежий пост даёт максимум 100 очков «свежести»,
    // через сутки — ~50, через 3 суток — ~25 и т.д.
    private const double FreshnessMax = 100.0;
    private const double FreshnessHalfLifeHours = 24.0;

    public double Score(PostSnapshot post, DateTimeOffset now)
    {
        var ageHours = Math.Max(0, (now - post.CreatedAt).TotalHours);
        var freshness = FreshnessMax / (1.0 + ageHours / FreshnessHalfLifeHours);
        return post.Likes * LikeWeight
             + post.Comments * CommentWeight
             + freshness;
    }
}
