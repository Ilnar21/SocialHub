using Microsoft.Extensions.Options;
using MongoDB.Driver;
using SocialHub.Notification.Application.Abstractions;
using SocialHub.Notification.Domain.Entities;
using SocialHub.Notification.Domain.Enums;
using SocialHub.Notification.Infrastructure.Processing;

namespace SocialHub.Notification.Infrastructure.Persistence;

public sealed class MongoNotificationEventRepository : INotificationEventRepository
{
    private readonly IMongoCollection<NotificationEventDocument> _events;
    private readonly NotificationProcessingOptions _processingOptions;
    private readonly NotificationRetentionOptions _retentionOptions;

    public MongoNotificationEventRepository(
        IMongoDatabase database,
        IOptions<MongoOptions> options,
        IOptions<NotificationProcessingOptions> processingOptions,
        IOptions<NotificationRetentionOptions> retentionOptions)
    {
        _events = database.GetCollection<NotificationEventDocument>(options.Value.EventsCollection);
        _processingOptions = processingOptions.Value;
        _retentionOptions = retentionOptions.Value;
        EnsureIndexes();
    }

    public async Task AddAsync(NotificationEvent notificationEvent, CancellationToken cancellationToken)
    {
        await _events.InsertOneAsync(NotificationEventDocument.FromDomain(notificationEvent), cancellationToken: cancellationToken);
    }

    public async Task<NotificationEvent?> TryTakeNextAsync(CancellationToken cancellationToken)
    {
        var retryableStatus = Builders<NotificationEventDocument>.Filter.In(
            x => x.Status,
            new[] { NotificationEventStatus.New, NotificationEventStatus.Failed });
        var hasAttemptsLeft = Builders<NotificationEventDocument>.Filter.Lt(x => x.AttemptCount, _processingOptions.MaxAttempts);
        var filter = Builders<NotificationEventDocument>.Filter.And(retryableStatus, hasAttemptsLeft);
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

        var completedRetentionIndex = Builders<NotificationEventDocument>.IndexKeys
            .Ascending(x => x.ProcessedAtUtc);

        var failedRetentionIndex = Builders<NotificationEventDocument>.IndexKeys
            .Ascending(x => x.LastAttemptAtUtc);

        var completedFilter = Builders<NotificationEventDocument>.Filter.And(
            Builders<NotificationEventDocument>.Filter.Eq(x => x.Status, NotificationEventStatus.Completed),
            Builders<NotificationEventDocument>.Filter.Exists(x => x.ProcessedAtUtc, true));

        var exhaustedFailedFilter = Builders<NotificationEventDocument>.Filter.And(
            Builders<NotificationEventDocument>.Filter.Eq(x => x.Status, NotificationEventStatus.Failed),
            Builders<NotificationEventDocument>.Filter.Gte(x => x.AttemptCount, _processingOptions.MaxAttempts),
            Builders<NotificationEventDocument>.Filter.Exists(x => x.LastAttemptAtUtc, true));

        _events.Indexes.CreateMany(new[]
        {
            new CreateIndexModel<NotificationEventDocument>(statusIndex),
            new CreateIndexModel<NotificationEventDocument>(sourceIndex),
            new CreateIndexModel<NotificationEventDocument>(
                completedRetentionIndex,
                new CreateIndexOptions<NotificationEventDocument>
                {
                    Name = "notification_events_completed_retention_ttl",
                    ExpireAfter = RetentionDays(_retentionOptions.CompletedEventDays),
                    PartialFilterExpression = completedFilter
                }),
            new CreateIndexModel<NotificationEventDocument>(
                failedRetentionIndex,
                new CreateIndexOptions<NotificationEventDocument>
                {
                    Name = "notification_events_failed_retention_ttl",
                    ExpireAfter = RetentionDays(_retentionOptions.FailedEventDays),
                    PartialFilterExpression = exhaustedFailedFilter
                })
        });
    }

    private static TimeSpan RetentionDays(int days)
    {
        return TimeSpan.FromDays(Math.Max(1, days));
    }
}
