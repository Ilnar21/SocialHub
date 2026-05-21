using Npgsql;
using SocialHub.Moderation.Application.Abstractions;

namespace SocialHub.Moderation.Infrastructure.Persistence;

public sealed class PostgresHealthCheck : IModerationStorageHealthCheck
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresHealthCheck(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand("select 1;", connection);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(result) == 1;
        }
        catch
        {
            return false;
        }
    }
}
