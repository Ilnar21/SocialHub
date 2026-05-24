namespace SocialHub.Post.Application.Abstractions;

public interface IPostVoteRepository
{
    Task<PostVoteTotals> GetTotalsAsync(Guid postId, CancellationToken cancellationToken);

    Task<int> GetUserVoteAsync(Guid postId, Guid userId, CancellationToken cancellationToken);

    Task SetVoteAsync(Guid postId, Guid userId, int value, DateTimeOffset now, CancellationToken cancellationToken);

    Task ClearVoteAsync(Guid postId, Guid userId, CancellationToken cancellationToken);
}

public sealed record PostVoteTotals(int Upvotes, int Downvotes)
{
    public int Score => Upvotes - Downvotes;
}
