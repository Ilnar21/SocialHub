using System.Collections.Concurrent;
using SocialHub.Notification.Application.Abstractions;
using NotificationEntity = SocialHub.Notification.Domain.Entities.Notification;

namespace SocialHub.Notification.Infrastructure.Persistence;

public sealed class InMemoryNotificationRepository : INotificationRepository
{
    private readonly ConcurrentDictionary<Guid, NotificationEntity> _notifications = new();

    public Task AddAsync(NotificationEntity notification, CancellationToken cancellationToken)
    {
        _notifications[notification.Id] = notification;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<NotificationEntity>> GetByRecipientAsync(Guid recipientUserId, CancellationToken cancellationToken)
    {
        var items = _notifications.Values
            .Where(x => x.RecipientUserId == recipientUserId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<NotificationEntity>>(items);
    }

    public Task<NotificationEntity?> GetByIdAsync(Guid notificationId, CancellationToken cancellationToken)
    {
        _notifications.TryGetValue(notificationId, out var notification);
        return Task.FromResult(notification);
    }

    public Task<int> CountUnreadAsync(Guid recipientUserId, CancellationToken cancellationToken)
    {
        var count = _notifications.Values.Count(x => x.RecipientUserId == recipientUserId && !x.IsRead);
        return Task.FromResult(count);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
