using Npgsql;
using SocialHub.Post.Application.Abstractions;
using SocialHub.Post.Domain;

namespace SocialHub.Post.Infrastructure.Persistence;

public sealed class PostgresPostMetadataRepository(NpgsqlDataSource dataSource) : IPostMetadataRepository
{
    public async Task<PostMetadata?> GetByIdAsync(Guid postId, CancellationToken cancellationToken)
    {
        const string sql = """
            select id, author_id, community_id, title, status, created_at, updated_at, deleted_at
            from post_metadata
            where id = @id
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("id", postId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadMetadata(reader) : null;
    }

    public async Task<IReadOnlyCollection<PostMetadata>> ListByCommunityAsync(
        Guid communityId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select id, author_id, community_id, title, status, created_at, updated_at, deleted_at
            from post_metadata
            where community_id = @community_id
            order by created_at desc
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("community_id", communityId);

        var posts = new List<PostMetadata>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            posts.Add(ReadMetadata(reader));
        }

        return posts;
    }

    public async Task<int> CountPublishedByAuthorInCommunitySinceAsync(
        Guid authorId,
        Guid communityId,
        DateTimeOffset since,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select count(*)
            from post_metadata
            where author_id = @author_id
              and community_id = @community_id
              and status = @status
              and created_at >= @since
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("author_id", authorId);
        command.Parameters.AddWithValue("community_id", communityId);
        command.Parameters.AddWithValue("status", PostStatus.Published.ToString());
        command.Parameters.AddWithValue("since", since);

        var count = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(count);
    }

    public async Task AddAsync(PostMetadata metadata, CancellationToken cancellationToken)
    {
        const string sql = """
            insert into post_metadata (
                id, author_id, community_id, title, status, created_at, updated_at, deleted_at
            )
            values (
                @id, @author_id, @community_id, @title, @status, @created_at, @updated_at, @deleted_at
            )
            """;

        await using var command = dataSource.CreateCommand(sql);
        AddMetadataParameters(command, metadata);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(PostMetadata metadata, CancellationToken cancellationToken)
    {
        const string sql = """
            update post_metadata
            set title = @title,
                status = @status,
                updated_at = @updated_at,
                deleted_at = @deleted_at
            where id = @id
            """;

        await using var command = dataSource.CreateCommand(sql);
        AddMetadataParameters(command, metadata);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> DeleteByCommunityAsync(Guid communityId, CancellationToken cancellationToken)
    {
        const string sql = """
            delete from post_metadata
            where community_id = @community_id
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("community_id", communityId);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddMetadataParameters(NpgsqlCommand command, PostMetadata metadata)
    {
        command.Parameters.AddWithValue("id", metadata.Id);
        command.Parameters.AddWithValue("author_id", metadata.AuthorId);
        command.Parameters.AddWithValue("community_id", metadata.CommunityId);
        command.Parameters.AddWithValue("title", metadata.Title);
        command.Parameters.AddWithValue("status", metadata.Status.ToString());
        command.Parameters.AddWithValue("created_at", metadata.CreatedAt);
        command.Parameters.AddWithValue("updated_at", (object?)metadata.UpdatedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("deleted_at", (object?)metadata.DeletedAt ?? DBNull.Value);
    }

    private static PostMetadata ReadMetadata(NpgsqlDataReader reader)
    {
        return new PostMetadata(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetGuid(2),
            reader.GetString(3),
            Enum.Parse<PostStatus>(reader.GetString(4)),
            reader.GetFieldValue<DateTimeOffset>(5),
            reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6),
            reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7));
    }
}
