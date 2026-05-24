using Npgsql;
using SocialHub.Post.Application.Abstractions;
using SocialHub.Post.Domain;

namespace SocialHub.Post.Infrastructure.Persistence;

public sealed class PostgresPostMediaRepository(NpgsqlDataSource dataSource) : IPostMediaRepository
{
    public async Task<PostMedia?> GetByIdAsync(Guid mediaId, CancellationToken cancellationToken)
    {
        const string sql = """
            select id, post_id, object_key, file_name, content_type, size_bytes, created_at
            from post_media
            where id = @id
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("id", mediaId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new PostMedia(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetInt64(5),
            reader.GetFieldValue<DateTimeOffset>(6));
    }

    public async Task<IReadOnlyCollection<PostMedia>> ListByPostIdAsync(Guid postId, CancellationToken cancellationToken)
    {
        const string sql = """
            select id, post_id, object_key, file_name, content_type, size_bytes, created_at
            from post_media
            where post_id = @post_id
            order by created_at, id
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("post_id", postId);

        var media = new List<PostMedia>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            media.Add(new PostMedia(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetInt64(5),
                reader.GetFieldValue<DateTimeOffset>(6)));
        }

        return media;
    }

    public async Task AddRangeAsync(IReadOnlyCollection<PostMedia> media, CancellationToken cancellationToken)
    {
        if (media.Count == 0)
        {
            return;
        }

        const string sql = """
            insert into post_media (
                id, post_id, object_key, file_name, content_type, size_bytes, created_at
            )
            values (
                @id, @post_id, @object_key, @file_name, @content_type, @size_bytes, @created_at
            )
            """;

        foreach (var item in media)
        {
            await using var command = dataSource.CreateCommand(sql);
            command.Parameters.AddWithValue("id", item.Id);
            command.Parameters.AddWithValue("post_id", item.PostId);
            command.Parameters.AddWithValue("object_key", item.ObjectKey);
            command.Parameters.AddWithValue("file_name", item.FileName);
            command.Parameters.AddWithValue("content_type", item.ContentType);
            command.Parameters.AddWithValue("size_bytes", item.Size);
            command.Parameters.AddWithValue("created_at", item.CreatedAt);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
