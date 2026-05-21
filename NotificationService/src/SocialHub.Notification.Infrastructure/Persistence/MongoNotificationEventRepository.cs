using Microsoft.Extensions.Options;
using MongoDB.Driver;
using SocialHub.Notification.Application.Abstractions;
using SocialHub.Notification.Domain.Entities;
using SocialHub.Notification.Domain.Enums;

namespace SocialHub.Notification.Infrastructure.Persistence;

public sealed class MongoNotificationEventRepository : INotificationEventRepository
{
    private readonly IMongoCollection<NotificationEventDocument> _events;

    public MongoNotificationEventRepository(IMongoDatabase database, IOptions<MongoOptions> options)
    {
        _events = database.GetCollection<NotificationEventDocument>(options.Value.EventsCollection);
        EnsureIndexes();
    }

    public async Task AddAsync(NotificationEvent notificationEvent, CancellationToken cancellationToken)
    {
        await _events.InsertOneAsync(NotificationEventDocument.FromDomain(notificationEvent), cancellationToken: cancellationToken);
    }

    public async Task<NotificationEvent?> TryTakeNextAsync(CancellationToken cancellationToken)
    {
        var filter = Builders<NotificationEventDocument>.Filter.Eq(x => x.Status, NotificationEventStatus.New);
        var update = Builders<NotificationEventDocument>.Update
            .Set(x => x.Status, NotificationEventStatus.Processing)
            .Inc(x => x.AttemptCount, 1)
            .Set(x => x.LastAttemptAtUtc, DateTime.UtcNow);
        var options = new FindOneAndUpdateOptions<NotificationEventDocument>
        {
            Sort = Builders<NotificationEventDocument>.Sort.Ascending(x => x.CreatedAtUtc),
            ReturnDocument = ReturnDocument.After
        };

        var document = await _events.FindOneAndUpdateAsync(filter, update, options, cancellationToken);
        return document?.ToDomain();
    }

    public async Task SaveAsync(NotificationEvent notificationEvent, CancellationToken cancellationToken)
    {
        var filter = Builders<NotificationEventDocument>.Filter.Eq(x => x.Id, notificationEvent.Id);
        await _events.ReplaceOneAsync(
            filter,
            NotificationEventDocument.FromDomain(notificationEvent),
            new ReplaceOptions { IsUpsert = false },
            cancellationToken);
    }

    private void EnsureIndexes()
    {
        var statusIndex = Builders<NotificationEventDocument>.IndexKeys
            .Ascending(x => x.Status)
            .Ascending(x => x.CreatedAtUtc);

        var sourceIndex = Builders<NotificationEventDocument>.IndexKeys
            .Ascending(x => x.SourceService)
            .Ascending(x => x.SourceEntityId);

        _events.Indexes.CreateMany(new[]
        {
            new CreateIndexModel<NotificationEventDocument>(statusIndex),
            new CreateIndexModel<NotificationEventDocument>(sourceIndex)
        });
    }
}
