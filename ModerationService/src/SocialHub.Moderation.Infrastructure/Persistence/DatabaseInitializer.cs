using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace SocialHub.Moderation.Infrastructure.Persistence;

public sealed class DatabaseInitializer : IHostedService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(NpgsqlDataSource dataSource, ILogger<DatabaseInitializer> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand(Sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Moderation database is unavailable during startup.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private const string Sql = """
        create table if not exists moderation_reports (
            id uuid primary key,
            reporter_user_id text not null,
            target_type text not null,
            target_id text not null,
            reason text not null,
            comment text null,
            status text not null,
            resolution_comment text null,
            resolved_by_user_id text null,
            created_at_utc timestamptz not null,
            updated_at_utc timestamptz not null
        );

        create index if not exists ix_moderation_reports_status on moderation_reports(status);

        create table if not exists user_blocks (
            id uuid primary key,
            blocked_user_id text not null,
            moderator_user_id text not null,
            reason text not null,
            blocked_at_utc timestamptz not null,
            expires_at_utc timestamptz not null
        );

        create index if not exists ix_user_blocks_user on user_blocks(blocked_user_id);

        create table if not exists audit_logs (
            id uuid primary key,
            actor_user_id text not null,
            actor_role text not null,
            action text not null,
            target_type text not null,
            target_id text not null,
            community_id text null,
            reason text not null,
            created_at_utc timestamptz not null
        );

        create index if not exists ix_audit_logs_actor_date on audit_logs(actor_user_id, created_at_utc);

        create table if not exists side_effect_failures (
            id uuid primary key,
            action text not null,
            target_type text not null,
            target_id text not null,
            service_name text not null,
            request_path text not null,
            error_message text not null,
            status text not null,
            created_at_utc timestamptz not null
        );

        create index if not exists ix_side_effect_failures_target on side_effect_failures(target_type, target_id);
        """;
}
