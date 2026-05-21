using MongoDB.Bson;
using MongoDB.Driver;
using SocialHub.Notification.Application.Abstractions;

namespace SocialHub.Notification.Infrastructure.Health;

public sealed class MongoNotificationHealthCheck : INotificationHealthCheck
{
    private readonly IMongoDatabase _database;

    public MongoNotificationHealthCheck(IMongoDatabase database)
    {
        _database = database;
    }

    public async Task<bool> IsMongoAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            var command = new BsonDocument("ping", 1);
            await _database.RunCommandAsync<BsonDocument>(command, cancellationToken: cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
