using System.Data;
using Npgsql;
using NpgsqlTypes;
using SocialHub.Moderation.Application.Abstractions;
using SocialHub.Moderation.Domain.Entities;
using SocialHub.Moderation.Domain.Enums;

namespace SocialHub.Moderation.Infrastructure.Persistence;

public sealed class PostgresModerationRepository : IModerationRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresModerationRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task AddReportAsync(ModerationReport report, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            insert into moderation_reports
                (id, reporter_user_id, target_type, target_id, reason, comment, status, created_at_utc, updated_at_utc)
            values
                (@id, @reporter_user_id, @target_type, @target_id, @reason, @comment, @status, @created_at_utc, @updated_at_utc);
            """, connection);

        AddReportParameters(command, report);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ModerationReport>> GetReportsAsync(ModerationReportStatus? status, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            select id, reporter_user_id, target_type, target_id, reason, comment, status,
                   resolution_comment, resolved_by_user_id, created_at_utc, updated_at_utc
            from moderation_reports
            where @status is null or status = @status
            order by created_at_utc desc;
            """, connection);

        command.Parameters.AddWithValue("status", status is null ? DBNull.Value : ToDatabaseStatus(status.Value));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var reports = new List<ModerationReport>();
        while (await reader.ReadAsync(cancellationToken))
        {
            reports.Add(ReadReport(reader));
        }

        return reports;
    }

    public async Task<ModerationReport?> GetReportAsync(Guid reportId, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await GetReportAsync(connection, null, reportId, cancellationToken);
    }

    public async Task<int> DeleteReportsByTargetAsync(string targetType, string targetId, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            delete from moderation_reports
            where target_type = @target_type
              and target_id = @target_id;
            """, connection);

        command.Parameters.AddWithValue("target_type", targetType.Trim().ToUpperInvariant());
        command.Parameters.AddWithValue("target_id", targetId.Trim());
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ResolveReportWithAuditAsync(ModerationReport report, AuditLog auditLog, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var command = new NpgsqlCommand("""
            update moderation_reports
            set status = @status,
                resolution_comment = @resolution_comment,
                resolved_by_user_id = @resolved_by_user_id,
                updated_at_utc = @updated_at_utc
            where id = @id;
            """, connection, transaction))
        {
            command.Parameters.AddWithValue("id", report.Id);
            command.Parameters.AddWithValue("status", ToDatabaseStatus(report.Status));
            command.Parameters.AddWithValue("resolution_comment", (object?)report.ResolutionComment ?? DBNull.Value);
            command.Parameters.AddWithValue("resolved_by_user_id", (object?)report.ResolvedByUserId ?? DBNull.Value);
            command.Parameters.AddWithValue("updated_at_utc", report.UpdatedAtUtc);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertAuditAsync(connection, transaction, auditLog, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task AddUserBlockWithAuditAsync(UserBlock block, AuditLog auditLog, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var command = new NpgsqlCommand("""
            insert into user_blocks (id, blocked_user_id, moderator_user_id, reason, blocked_at_utc, expires_at_utc)
            values (@id, @blocked_user_id, @moderator_user_id, @reason, @blocked_at_utc, @expires_at_utc);
            """, connection, transaction))
        {
            command.Parameters.AddWithValue("id", block.Id);
            command.Parameters.AddWithValue("blocked_user_id", block.BlockedUserId);
            command.Parameters.AddWithValue("moderator_user_id", block.ModeratorUserId);
            command.Parameters.AddWithValue("reason", block.Reason);
            command.Parameters.AddWithValue("blocked_at_utc", block.BlockedAtUtc);
            command.Parameters.AddWithValue("expires_at_utc", block.ExpiresAtUtc);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertAuditAsync(connection, transaction, auditLog, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<AuditLog> AddAuditAsync(AuditLog auditLog, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await InsertAuditAsync(connection, transaction, auditLog, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return auditLog;
    }

    public async Task<IReadOnlyCollection<AuditLog>> GetAuditAsync(string? actorUserId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            select id, actor_user_id, actor_role, action, target_type, target_id, community_id, reason, created_at_utc
            from audit_logs
            where (@actor_user_id is null or actor_user_id = @actor_user_id)
              and (@from is null or created_at_utc >= @from)
              and (@to is null or created_at_utc <= @to)
            order by created_at_utc desc;
            """, connection);

        command.Parameters.Add("actor_user_id", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(actorUserId) ? DBNull.Value : actorUserId.Trim();
        command.Parameters.Add("from", NpgsqlDbType.TimestampTz).Value =
            from is null ? DBNull.Value : from.Value;
        command.Parameters.Add("to", NpgsqlDbType.TimestampTz).Value =
            to is null ? DBNull.Value : to.Value;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var entries = new List<AuditLog>();
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(ReadAudit(reader));
        }

        return entries;
    }

    public async Task AddSideEffectFailuresAsync(IReadOnlyCollection<SideEffectFailure> failures, CancellationToken cancellationToken)
    {
        if (failures.Count == 0)
        {
            return;
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var failure in failures)
        {
            await using var command = new NpgsqlCommand("""
                insert into side_effect_failures
                    (id, action, target_type, target_id, service_name, request_path, error_message, status, created_at_utc)
                values
                    (@id, @action, @target_type, @target_id, @service_name, @request_path, @error_message, 'FAILED', @created_at_utc);
                """, connection, transaction);

            command.Parameters.AddWithValue("id", failure.Id);
            command.Parameters.AddWithValue("action", failure.Action);
            command.Parameters.AddWithValue("target_type", failure.TargetType);
            command.Parameters.AddWithValue("target_id", failure.TargetId);
            command.Parameters.AddWithValue("service_name", failure.ServiceName);
            command.Parameters.AddWithValue("request_path", failure.RequestPath);
            command.Parameters.AddWithValue("error_message", failure.ErrorMessage);
            command.Parameters.AddWithValue("created_at_utc", failure.CreatedAtUtc);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<ModerationReport?> GetReportAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid reportId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            select id, reporter_user_id, target_type, target_id, reason, comment, status,
                   resolution_comment, resolved_by_user_id, created_at_utc, updated_at_utc
            from moderation_reports
            where id = @id;
            """, connection, transaction);

        command.Parameters.AddWithValue("id", reportId);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadReport(reader) : null;
    }

    private static void AddReportParameters(NpgsqlCommand command, ModerationReport report)
    {
        command.Parameters.AddWithValue("id", report.Id);
        command.Parameters.AddWithValue("reporter_user_id", report.ReporterUserId);
        command.Parameters.AddWithValue("target_type", report.TargetType);
        command.Parameters.AddWithValue("target_id", report.TargetId);
        command.Parameters.AddWithValue("reason", report.Reason);
        command.Parameters.AddWithValue("comment", (object?)report.Comment ?? DBNull.Value);
        command.Parameters.AddWithValue("status", ToDatabaseStatus(report.Status));
        command.Parameters.AddWithValue("created_at_utc", report.CreatedAtUtc);
        command.Parameters.AddWithValue("updated_at_utc", report.UpdatedAtUtc);
    }

    private static async Task InsertAuditAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, AuditLog audit, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            insert into audit_logs
                (id, actor_user_id, actor_role, action, target_type, target_id, community_id, reason, created_at_utc)
            values
                (@id, @actor_user_id, @actor_role, @action, @target_type, @target_id, @community_id, @reason, @created_at_utc);
            """, connection, transaction);

        command.Parameters.AddWithValue("id", audit.Id);
        command.Parameters.AddWithValue("actor_user_id", audit.ActorUserId);
        command.Parameters.AddWithValue("actor_role", audit.ActorRole);
        command.Parameters.AddWithValue("action", audit.Action);
        command.Parameters.AddWithValue("target_type", audit.TargetType);
        command.Parameters.AddWithValue("target_id", audit.TargetId);
        command.Parameters.AddWithValue("community_id", (object?)audit.CommunityId ?? DBNull.Value);
        command.Parameters.AddWithValue("reason", audit.Reason);
        command.Parameters.AddWithValue("created_at_utc", audit.CreatedAtUtc);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static ModerationReport ReadReport(IDataRecord reader) => new(
        reader.GetGuid(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetString(4),
        reader.IsDBNull(5) ? null : reader.GetString(5),
        FromDatabaseStatus(reader.GetString(6)),
        reader.IsDBNull(7) ? null : reader.GetString(7),
        reader.IsDBNull(8) ? null : reader.GetString(8),
        ReadTimestamp(reader, 9),
        ReadTimestamp(reader, 10));

    private static AuditLog ReadAudit(IDataRecord reader) => new(
        reader.GetGuid(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetString(4),
        reader.GetString(5),
        reader.IsDBNull(6) ? null : reader.GetString(6),
        reader.GetString(7),
        ReadTimestamp(reader, 8));

    private static ModerationReportStatus FromDatabaseStatus(string status) => status switch
    {
        "NEW" => ModerationReportStatus.New,
        "RESOLVED" => ModerationReportStatus.Resolved,
        _ => ModerationReportStatus.New
    };

    private static string ToDatabaseStatus(ModerationReportStatus status) => status switch
    {
        ModerationReportStatus.New => "NEW",
        ModerationReportStatus.Resolved => "RESOLVED",
        _ => status.ToString().ToUpperInvariant()
    };

    private static DateTimeOffset ReadTimestamp(IDataRecord reader, int ordinal)
    {
        var value = reader.GetValue(ordinal);
        return value switch
        {
            DateTimeOffset dateTimeOffset => dateTimeOffset,
            DateTime dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)),
            _ => DateTimeOffset.Parse(Convert.ToString(value)!, null, System.Globalization.DateTimeStyles.AssumeUniversal)
        };
    }
}
