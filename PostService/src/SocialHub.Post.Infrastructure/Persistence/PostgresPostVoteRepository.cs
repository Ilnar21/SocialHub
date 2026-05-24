using Npgsql;
using SocialHub.Post.Application.Abstractions;

namespace SocialHub.Post.Infrastructure.Persistence;

public sealed class PostgresPostVoteRepository(NpgsqlDataSource dataSource) : IPostVoteRepository
{
    public async Task<PostVoteTotals> GetTotalsAsync(Guid postId, CancellationToken cancellationToken)
    {
        const string sql = """
            select
                count(*) filter (where value = 1) as upvotes,
                count(*) filter (where value = -1) as downvotes
            from post_votes
            where post_id = @post_id
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("post_id", postId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new PostVoteTotals(0, 0);
        }

        return new PostVoteTotals(
            Convert.ToInt32(reader.GetInt64(0)),
            Convert.ToInt32(reader.GetInt64(1)));
    }

    public async Task<int> GetUserVoteAsync(Guid postId, Guid userId, CancellationToken cancellationToken)
    {
        const string sql = """
            select value
            from post_votes
            where post_id = @post_id and user_id = @user_id
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("post_id", postId);
        command.Parameters.AddWithValue("user_id", userId);

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null ? 0 : Convert.ToInt32(value);
    }

    public async Task SetVoteAsync(
        Guid postId,
        Guid userId,
        int value,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into post_votes (post_id, user_id, value, created_at, updated_at)
            values (@post_id, @user_id, @value, @now, @now)
            on conflict (post_id, user_id) do update
            set value = excluded.value,
                updated_at = excluded.updated_at
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("post_id", postId);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("value", value);
        command.Parameters.AddWithValue("now", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ClearVoteAsync(Guid postId, Guid userId, CancellationToken cancellationToken)
    {
        const string sql = """
            delete from post_votes
            where post_id = @post_id and user_id = @user_id
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("post_id", postId);
        command.Parameters.AddWithValue("user_id", userId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
