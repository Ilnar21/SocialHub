using Npgsql;
using SocialHub.Post.Application.Abstractions;
using SocialHub.Post.Domain;

namespace SocialHub.Post.Infrastructure.Persistence;

public sealed class PostgresPostContentRepository(NpgsqlDataSource dataSource) : IPostContentRepository
{
    public async Task<PostContent?> GetByPostIdAsync(Guid postId, CancellationToken cancellationToken)
    {
        const string sql = """
            select post_id, text, updated_at
            from post_contents
            where post_id = @post_id
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("post_id", postId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new PostContent(reader.GetGuid(0), reader.GetString(1), reader.GetFieldValue<DateTimeOffset>(2))
            : null;
    }

    public async Task SaveAsync(PostContent content, CancellationToken cancellationToken)
    {
        const string sql = """
            insert into post_contents (post_id, text, updated_at)
            values (@post_id, @text, @updated_at)
            """;

        await using var command = dataSource.CreateCommand(sql);
        AddParameters(command, content);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(PostContent content, CancellationToken cancellationToken)
    {
        const string sql = """
            update post_contents
            set text = @text,
                updated_at = @updated_at
            where post_id = @post_id
            """;

        await using var command = dataSource.CreateCommand(sql);
        AddParameters(command, content);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameters(NpgsqlCommand command, PostContent content)
    {
        command.Parameters.AddWithValue("post_id", content.PostId);
        command.Parameters.AddWithValue("text", content.Text);
        command.Parameters.AddWithValue("updated_at", content.UpdatedAt);
    }
}
