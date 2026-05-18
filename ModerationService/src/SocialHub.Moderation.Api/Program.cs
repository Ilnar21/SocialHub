using System.Data;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("ModerationDatabase")
    ?? throw new InvalidOperationException("Connection string 'ModerationDatabase' is required.");

builder.Services.AddSingleton(_ => new NpgsqlDataSourceBuilder(connectionString).Build());
builder.Services.AddHttpClient("post", client => ConfigureBaseAddress(client, builder.Configuration["ExternalServices:PostBaseUrl"]));
builder.Services.AddHttpClient("auth", client => ConfigureBaseAddress(client, builder.Configuration["ExternalServices:AuthBaseUrl"]));
builder.Services.AddHttpClient("notifications", client => ConfigureBaseAddress(client, builder.Configuration["ExternalServices:NotificationBaseUrl"]));

var app = builder.Build();

await EnsureDatabaseAsync(app.Services.GetRequiredService<NpgsqlDataSource>(), app.Logger);

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "moderation-service" }));

app.MapPost("/api/reports", async (
    CreateReportRequest request,
    HttpContext http,
    NpgsqlDataSource db,
    CancellationToken cancellationToken) =>
{
    var reporterUserId = GetRequiredUserId(http);
    if (reporterUserId is null)
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(request.TargetType) || string.IsNullOrWhiteSpace(request.TargetId) || string.IsNullOrWhiteSpace(request.Reason))
    {
        return Results.BadRequest(new ErrorResponse("report_invalid", "Target type, target id and reason are required."));
    }

    var now = DateTimeOffset.UtcNow;
    var report = new ReportResponse(
        Guid.NewGuid(),
        reporterUserId,
        request.TargetType.Trim().ToUpperInvariant(),
        request.TargetId.Trim(),
        request.Reason.Trim(),
        request.Comment?.Trim(),
        "NEW",
        null,
        null,
        now,
        now);

    await using var connection = await db.OpenConnectionAsync(cancellationToken);
    await using var command = new NpgsqlCommand("""
        insert into moderation_reports
            (id, reporter_user_id, target_type, target_id, reason, comment, status, created_at_utc, updated_at_utc)
        values
            (@id, @reporter_user_id, @target_type, @target_id, @reason, @comment, @status, @created_at_utc, @updated_at_utc);
        """, connection);

    command.Parameters.AddWithValue("id", report.Id);
    command.Parameters.AddWithValue("reporter_user_id", report.ReporterUserId);
    command.Parameters.AddWithValue("target_type", report.TargetType);
    command.Parameters.AddWithValue("target_id", report.TargetId);
    command.Parameters.AddWithValue("reason", report.Reason);
    command.Parameters.AddWithValue("comment", (object?)report.Comment ?? DBNull.Value);
    command.Parameters.AddWithValue("status", report.Status);
    command.Parameters.AddWithValue("created_at_utc", report.CreatedAtUtc);
    command.Parameters.AddWithValue("updated_at_utc", report.UpdatedAtUtc);

    await command.ExecuteNonQueryAsync(cancellationToken);
    return Results.Created($"/api/reports/{report.Id}", report);
});

app.MapGet("/api/reports", async (
    string? status,
    NpgsqlDataSource db,
    CancellationToken cancellationToken) =>
{
    await using var connection = await db.OpenConnectionAsync(cancellationToken);
    var sql = """
        select id, reporter_user_id, target_type, target_id, reason, comment, status,
               resolution_comment, resolved_by_user_id, created_at_utc, updated_at_utc
        from moderation_reports
        where @status is null or status = @status
        order by created_at_utc desc;
        """;

    await using var command = new NpgsqlCommand(sql, connection);
    command.Parameters.AddWithValue("status", string.IsNullOrWhiteSpace(status) ? DBNull.Value : status.Trim().ToUpperInvariant());
    await using var reader = await command.ExecuteReaderAsync(cancellationToken);

    var reports = new List<ReportResponse>();
    while (await reader.ReadAsync(cancellationToken))
    {
        reports.Add(ReadReport(reader));
    }

    return Results.Ok(reports);
});

app.MapPost("/api/reports/{reportId:guid}/resolve/delete-post", async (
    Guid reportId,
    ResolveReportRequest request,
    HttpContext http,
    NpgsqlDataSource db,
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    var moderatorUserId = GetRequiredUserId(http);
    if (moderatorUserId is null)
    {
        return Results.Unauthorized();
    }

    if (!IsPlatformModerator(http))
    {
        return Results.Forbid();
    }

    await using var connection = await db.OpenConnectionAsync(cancellationToken);
    await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

    var report = await GetReportInTransactionAsync(connection, reportId, transaction, cancellationToken);
    if (report is null)
    {
        return Results.NotFound(new ErrorResponse("report_not_found", "Report was not found."));
    }

    var now = DateTimeOffset.UtcNow;
    await using (var command = new NpgsqlCommand("""
        update moderation_reports
        set status = 'RESOLVED',
            resolution_comment = @resolution_comment,
            resolved_by_user_id = @resolved_by_user_id,
            updated_at_utc = @updated_at_utc
        where id = @id;
        """, connection, transaction))
    {
        command.Parameters.AddWithValue("id", reportId);
        command.Parameters.AddWithValue("resolution_comment", request.Comment?.Trim() ?? "Post deleted by platform moderator.");
        command.Parameters.AddWithValue("resolved_by_user_id", moderatorUserId);
        command.Parameters.AddWithValue("updated_at_utc", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    await InsertAuditAsync(
        connection,
        transaction,
        new CreateAuditRequest("POST_DELETED", "POST", report.TargetId, request.Comment ?? report.Reason, null, "PLATFORM_MODERATOR"),
        moderatorUserId,
        now,
        cancellationToken);

    await transaction.CommitAsync(cancellationToken);

    _ = TryPostAsync(httpClientFactory.CreateClient("post"), $"/api/posts/{report.TargetId}/moderation-delete", new { reason = request.Comment ?? report.Reason }, loggerFactory.CreateLogger("PostService"), CancellationToken.None);
    _ = TryPostAsync(httpClientFactory.CreateClient("notifications"), "/api/notifications", new { type = "POST_DELETED", payload = new { report.TargetId, report.Reason } }, loggerFactory.CreateLogger("Notification"), CancellationToken.None);

    return Results.Ok(await GetReportAsync(connection, reportId, cancellationToken));
});

app.MapPost("/api/users/{userId}/blocks", async (
    string userId,
    BlockUserRequest request,
    HttpContext http,
    NpgsqlDataSource db,
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    var moderatorUserId = GetRequiredUserId(http);
    if (moderatorUserId is null)
    {
        return Results.Unauthorized();
    }

    if (!IsPlatformModerator(http))
    {
        return Results.Forbid();
    }

    if (string.IsNullOrWhiteSpace(request.Reason) || request.DurationDays <= 0)
    {
        return Results.BadRequest(new ErrorResponse("block_invalid", "Reason and positive duration are required."));
    }

    var now = DateTimeOffset.UtcNow;
    var block = new BlockResponse(Guid.NewGuid(), userId.Trim(), moderatorUserId, request.Reason.Trim(), now, now.AddDays(request.DurationDays));

    await using var connection = await db.OpenConnectionAsync(cancellationToken);
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

    await InsertAuditAsync(
        connection,
        transaction,
        new CreateAuditRequest("USER_BLOCKED", "USER", block.BlockedUserId, block.Reason, null, "PLATFORM_MODERATOR"),
        moderatorUserId,
        now,
        cancellationToken);

    await transaction.CommitAsync(cancellationToken);

    _ = TryPostAsync(httpClientFactory.CreateClient("auth"), $"/api/users/{block.BlockedUserId}/status", new { status = "BLOCKED", expiresAtUtc = block.ExpiresAtUtc }, loggerFactory.CreateLogger("AuthService"), CancellationToken.None);
    _ = TryPostAsync(httpClientFactory.CreateClient("notifications"), "/api/notifications", new { type = "USER_BLOCKED", recipientUserId = block.BlockedUserId, payload = block }, loggerFactory.CreateLogger("Notification"), CancellationToken.None);

    return Results.Created($"/api/users/{block.BlockedUserId}/blocks/{block.Id}", block);
});

app.MapPost("/api/audit", async (
    CreateAuditRequest request,
    HttpContext http,
    NpgsqlDataSource db,
    CancellationToken cancellationToken) =>
{
    var actorUserId = GetRequiredUserId(http);
    if (actorUserId is null)
    {
        return Results.Unauthorized();
    }

    var now = DateTimeOffset.UtcNow;
    await using var connection = await db.OpenConnectionAsync(cancellationToken);
    await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
    var audit = await InsertAuditAsync(connection, transaction, request, actorUserId, now, cancellationToken);
    await transaction.CommitAsync(cancellationToken);

    return Results.Created($"/api/audit/{audit.Id}", audit);
});

app.MapGet("/api/audit", async (
    string? actorUserId,
    DateTimeOffset? from,
    DateTimeOffset? to,
    NpgsqlDataSource db,
    CancellationToken cancellationToken) =>
{
    await using var connection = await db.OpenConnectionAsync(cancellationToken);
    await using var command = new NpgsqlCommand("""
        select id, actor_user_id, actor_role, action, target_type, target_id, community_id, reason, created_at_utc
        from audit_logs
        where (@actor_user_id is null or actor_user_id = @actor_user_id)
          and (@from is null or created_at_utc >= @from)
          and (@to is null or created_at_utc <= @to)
        order by created_at_utc desc;
        """, connection);

    command.Parameters.AddWithValue("actor_user_id", string.IsNullOrWhiteSpace(actorUserId) ? DBNull.Value : actorUserId.Trim());
    command.Parameters.AddWithValue("from", from is null ? DBNull.Value : from.Value);
    command.Parameters.AddWithValue("to", to is null ? DBNull.Value : to.Value);

    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
    var entries = new List<AuditResponse>();
    while (await reader.ReadAsync(cancellationToken))
    {
        entries.Add(ReadAudit(reader));
    }

    return Results.Ok(entries);
});

app.MapGet("/api/private-messages/{**_}", () =>
    Results.Problem("Доступ к личным сообщениям запрещён", statusCode: StatusCodes.Status403Forbidden));

app.Run();

static void ConfigureBaseAddress(HttpClient client, string? baseUrl)
{
    if (!string.IsNullOrWhiteSpace(baseUrl))
    {
        client.BaseAddress = new Uri(baseUrl);
    }
}

static string? GetRequiredUserId(HttpContext http)
{
    var userId = http.Request.Headers["X-User-Id"].FirstOrDefault();
    return string.IsNullOrWhiteSpace(userId) ? null : userId.Trim();
}

static bool IsPlatformModerator(HttpContext http)
{
    var role = http.Request.Headers["X-User-Role"].FirstOrDefault();
    return role is not null && (role.Equals("PLATFORM_MODERATOR", StringComparison.OrdinalIgnoreCase)
        || role.Equals("MODERATOR", StringComparison.OrdinalIgnoreCase));
}

static async Task EnsureDatabaseAsync(NpgsqlDataSource db, ILogger logger)
{
    try
    {
        await using var connection = await db.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
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
            """, connection);
        await command.ExecuteNonQueryAsync();
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Moderation database is unavailable during startup.");
    }
}

static async Task<ReportResponse?> GetReportAsync(NpgsqlConnection connection, Guid reportId, CancellationToken cancellationToken)
{
    await using var command = new NpgsqlCommand("""
        select id, reporter_user_id, target_type, target_id, reason, comment, status,
               resolution_comment, resolved_by_user_id, created_at_utc, updated_at_utc
        from moderation_reports
        where id = @id;
        """, connection);
    command.Parameters.AddWithValue("id", reportId);

    await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
    return await reader.ReadAsync(cancellationToken) ? ReadReport(reader) : null;
}

static async Task<ReportResponse?> GetReportInTransactionAsync(NpgsqlConnection connection, Guid reportId, NpgsqlTransaction transaction, CancellationToken cancellationToken)
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

static ReportResponse ReadReport(IDataRecord reader) => new(
    reader.GetGuid(0),
    reader.GetString(1),
    reader.GetString(2),
    reader.GetString(3),
    reader.GetString(4),
    reader.IsDBNull(5) ? null : reader.GetString(5),
    reader.GetString(6),
    reader.IsDBNull(7) ? null : reader.GetString(7),
    reader.IsDBNull(8) ? null : reader.GetString(8),
    ReadTimestamp(reader, 9),
    ReadTimestamp(reader, 10));

static async Task<AuditResponse> InsertAuditAsync(
    NpgsqlConnection connection,
    NpgsqlTransaction transaction,
    CreateAuditRequest request,
    string actorUserId,
    DateTimeOffset createdAtUtc,
    CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(request.Action) || string.IsNullOrWhiteSpace(request.TargetType) || string.IsNullOrWhiteSpace(request.TargetId))
    {
        throw new BadHttpRequestException("Action, target type and target id are required.");
    }

    var audit = new AuditResponse(
        Guid.NewGuid(),
        actorUserId,
        request.ActorRole?.Trim() ?? "UNKNOWN",
        request.Action.Trim().ToUpperInvariant(),
        request.TargetType.Trim().ToUpperInvariant(),
        request.TargetId.Trim(),
        request.CommunityId?.Trim(),
        request.Reason?.Trim() ?? "",
        createdAtUtc);

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

    return audit;
}

static AuditResponse ReadAudit(IDataRecord reader) => new(
    reader.GetGuid(0),
    reader.GetString(1),
    reader.GetString(2),
    reader.GetString(3),
    reader.GetString(4),
    reader.GetString(5),
    reader.IsDBNull(6) ? null : reader.GetString(6),
    reader.GetString(7),
    ReadTimestamp(reader, 8));

static DateTimeOffset ReadTimestamp(IDataRecord reader, int ordinal)
{
    var value = reader.GetValue(ordinal);
    return value switch
    {
        DateTimeOffset dateTimeOffset => dateTimeOffset,
        DateTime dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)),
        _ => DateTimeOffset.Parse(Convert.ToString(value)!, null, System.Globalization.DateTimeStyles.AssumeUniversal)
    };
}

static async Task TryPostAsync<T>(HttpClient client, string path, T body, ILogger logger, CancellationToken cancellationToken)
{
    if (client.BaseAddress is null)
    {
        return;
    }

    try
    {
        await client.PostAsJsonAsync(path, body, cancellationToken);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "External moderation side effect failed for {Path}", path);
    }
}

sealed record CreateReportRequest(string TargetType, string TargetId, string Reason, string? Comment);
sealed record ResolveReportRequest(string? Comment);
sealed record BlockUserRequest(int DurationDays, string Reason);
sealed record CreateAuditRequest(string Action, string TargetType, string TargetId, string? Reason, string? CommunityId, string? ActorRole);
sealed record ErrorResponse(string Code, string Message);

sealed record ReportResponse(
    Guid Id,
    string ReporterUserId,
    string TargetType,
    string TargetId,
    string Reason,
    string? Comment,
    string Status,
    string? ResolutionComment,
    string? ResolvedByUserId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

sealed record BlockResponse(
    Guid Id,
    string BlockedUserId,
    string ModeratorUserId,
    string Reason,
    DateTimeOffset BlockedAtUtc,
    DateTimeOffset ExpiresAtUtc);

sealed record AuditResponse(
    Guid Id,
    string ActorUserId,
    string ActorRole,
    string Action,
    string TargetType,
    string TargetId,
    string? CommunityId,
    string Reason,
    DateTimeOffset CreatedAtUtc);
