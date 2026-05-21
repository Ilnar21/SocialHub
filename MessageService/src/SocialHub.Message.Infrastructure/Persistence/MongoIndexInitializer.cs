using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace SocialHub.Message.Infrastructure.Persistence;

public sealed class MongoIndexInitializer : IHostedService
{
    private readonly IMongoCollection<DialogDocument> _dialogs;
    private readonly ILogger<MongoIndexInitializer> _logger;

    public MongoIndexInitializer(IMongoDatabase database, MongoOptions options, ILogger<MongoIndexInitializer> logger)
    {
        _dialogs = database.GetCollection<DialogDocument>(options.DialogsCollection);
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var indexes = new[]
            {
                new CreateIndexModel<DialogDocument>(
                    Builders<DialogDocument>.IndexKeys.Ascending(x => x.ParticipantUserIds),
                    new CreateIndexOptions { Name = "ix_dialogs_participants" }),
                new CreateIndexModel<DialogDocument>(
                    Builders<DialogDocument>.IndexKeys.Descending(x => x.LastMessageAt),
                    new CreateIndexOptions { Name = "ix_dialogs_last_message_at" }),
                new CreateIndexModel<DialogDocument>(
                    Builders<DialogDocument>.IndexKeys.Descending("Messages.SentAt"),
                    new CreateIndexOptions { Name = "ix_dialogs_messages_sent_at" })
            };

            await _dialogs.Indexes.CreateManyAsync(indexes, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MongoDB indexes were not created.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
