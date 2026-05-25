namespace SocialHub.Feed.Application.Models.Feed;

public sealed record FeedQueryOptions(FeedSortMode Sort, FeedPeriod Period)
{
    public static FeedQueryOptions Default { get; } = new(FeedSortMode.Popular, FeedPeriod.All);

    public string CacheSortKey => Sort.ToString().ToLowerInvariant();

    public string CachePeriodKey => Period.ToString().ToLowerInvariant();

    public static FeedQueryOptions From(string? sort, string? period)
    {
        return new(ParseSort(sort), ParsePeriod(period));
    }

    private static FeedSortMode ParseSort(string? value)
    {
        return Normalize(value) switch
        {
            "new" or "newest" or "latest" => FeedSortMode.Newest,
            "comments" or "discussed" or "commented" => FeedSortMode.Discussed,
            _ => FeedSortMode.Popular
        };
    }

    private static FeedPeriod ParsePeriod(string? value)
    {
        return Normalize(value) switch
        {
            "day" or "today" => FeedPeriod.Day,
            "week" => FeedPeriod.Week,
            "month" => FeedPeriod.Month,
            "year" => FeedPeriod.Year,
            _ => FeedPeriod.All
        };
    }

    private static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();
}

public enum FeedSortMode
{
    Popular,
    Newest,
    Discussed
}

public enum FeedPeriod
{
    All,
    Day,
    Week,
    Month,
    Year
}
