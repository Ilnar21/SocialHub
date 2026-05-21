using MongoDB.Bson;
using MongoDB.Driver;
using SocialHub.Message.Application.Abstractions;

namespace SocialHub.Message.Infrastructure.Persistence;

public sealed class MongoHealthCheck : IMessageStorageHealthCheck
{
    private readonly IMongoDatabase _database;

    public MongoHealthCheck(IMongoDatabase database)
    {
        _database = database;
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken);
            return result.TryGetValue("ok", out var ok) && ok.ToDouble() == 1d;
        }
        catch
        {
            return false;
        }
    }
}
