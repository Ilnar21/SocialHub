using Microsoft.Extensions.Hosting;
using Npgsql;

namespace SocialHub.Post.Infrastructure.Persistence;

public sealed class PostgresDatabaseInitializer(NpgsqlDataSource dataSource) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            create table if not exists post_metadata (
                id uuid primary key,
                author_id uuid not null,
                community_id uuid not null,
                title text not null,
                status text not null,
                created_at timestamptz not null,
                updated_at timestamptz null,
                deleted_at timestamptz null
            );

            create table if not exists post_contents (
                post_id uuid primary key references post_metadata(id) on delete cascade,
                text text not null,
                updated_at timestamptz not null
            );

            create table if not exists post_media (
                id uuid primary key,
                post_id uuid not null references post_metadata(id) on delete cascade,
                object_key text not null,
                file_name text not null,
                content_type text not null,
                size_bytes bigint not null,
                created_at timestamptz not null
            );

            create index if not exists ix_post_metadata_community_created_at
                on post_metadata (community_id, created_at desc);

            create index if not exists ix_post_metadata_author_community_created_at
                on post_metadata (author_id, community_id, created_at desc)
                where status = 'Published';

            create index if not exists ix_post_media_post_id
                on post_media (post_id);
            """;

        await using var command = dataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
