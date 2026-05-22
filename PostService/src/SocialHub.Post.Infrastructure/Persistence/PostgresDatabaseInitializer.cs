using Microsoft.Extensions.Hosting;
using Npgsql;

namespace SocialHub.Post.Infrastructure.Persistence;

public sealed class PostgresDatabaseInitializer : IHostedService
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresDatabaseInitializer(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

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

            create index if not exists ix_post_metadata_community_created_at
                on post_metadata (community_id, created_at desc);

            create index if not exists ix_post_metadata_author_community_created_at
                on post_metadata (author_id, community_id, created_at desc)
                where status = 'Published';
            """;

        await using var command = _dataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
