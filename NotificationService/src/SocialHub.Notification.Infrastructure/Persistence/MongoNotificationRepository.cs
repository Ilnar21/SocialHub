using Microsoft.Extensions.Options;
using MongoDB.Driver;
using SocialHub.Notification.Application.Abstractions;
using NotificationEntity = SocialHub.Notification.Domain.Entities.Notification;

namespace SocialHub.Notification.Infrastructure.Persistence;

public sealed class MongoNotificationRepository : INotificationRepository
{
    private readonly IMongoCollection<NotificationDocument> _notifications;

    public MongoNotificationRepository(IMongoDatabase database, IOptions<MongoOptions> options)
    {
        _notifications = database.GetCollection<NotificationDocument>(options.Value.NotificationsCollection);
        EnsureIndexes();
    }

    public async Task AddAsync(NotificationEntity notification, CancellationToken cancellationToken)
    {
        await _notifications.InsertOneAsync(NotificationDocument.FromDomain(notification), cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyCollection<NotificationEntity>> GetByRecipientAsync(Guid recipientUserId, CancellationToken cancellationToken)
    {
        var documents = await _notifications
            .Find(x => x.RecipientUserId == recipientUserId)
            .SortByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return documents.Select(x => x.ToDomain()).ToArray();
    }

    public async Task<NotificationEntity?> GetByIdAsync(Guid notificationId, CancellationToken cancellationToken)
    {
        var document = await _notifications
            .Find(x => x.Id == notificationId)
            .FirstOrDefaultAsync(cancellationToken);

        return document?.ToDomain();
    }

    public async Task<int> CountUnreadAsync(Guid recipientUserId, CancellationToken cancellationToken)
    {
        var count = await _notifications.CountDocumentsAsync(
            x => x.RecipientUserId == recipientUserId && !x.IsRead,
            cancellationToken: cancellationToken);

        return (int)count;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }

    private void EnsureIndexes()
    {
        var inboxIndex = Builders<NotificationDocument>.IndexKeys
            .Ascending(x => x.RecipientUserId)
            .Descending(x => x.CreatedAtUtc);

        var unreadIndex = Builders<NotificationDocument>.IndexKeys
            .Ascending(x => x.RecipientUserId)
            .Ascending(x => x.IsRead);

        _notifications.Indexes.CreateMany(new[]
        {
            new CreateIndexModel<NotificationDocument>(inboxIndex),
            new CreateIndexModel<NotificationDocument>(unreadIndex)
        });
    }
}
