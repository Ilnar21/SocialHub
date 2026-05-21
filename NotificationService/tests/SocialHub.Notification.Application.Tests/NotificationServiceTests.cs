using SocialHub.Notification.Application.Abstractions;
using SocialHub.Notification.Application.Exceptions;
using SocialHub.Notification.Application.Models.Events;
using SocialHub.Notification.Application.Models.Notifications;
using SocialHub.Notification.Domain.Entities;
using SocialHub.Notification.Domain.Enums;
using NotificationAppService = SocialHub.Notification.Application.Services.NotificationService;
using NotificationEntity = SocialHub.Notification.Domain.Entities.Notification;

namespace SocialHub.Notification.Application.Tests;

public sealed class NotificationServiceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task CreateEventAsync_StoresPendingEvent_WithRecipientEmail()
    {
        var eventRepository = new FakeNotificationEventRepository();
        var service = CreateService(eventRepository: eventRepository);

        var response = await service.CreateEventAsync(
            new CreateNotificationEventRequest(
                UserId,
                NotificationType.MessageReceived,
                "New message",
                "You have a message",
                "MessageService",
                RecipientEmail: "user@example.com"),
            CancellationToken.None);

        Assert.Equal(NotificationEventStatus.New, response.Status);
        Assert.Single(eventRepository.Events);
        Assert.Equal("user@example.com", eventRepository.Events[0].RecipientEmail);
    }

    [Fact]
    public async Task GetCurrentUserNotificationsAsync_ReturnsOnlyCurrentUserItems()
    {
        var repository = new FakeNotificationRepository();
        repository.Notifications.Add(new NotificationEntity(UserId, NotificationType.MessageReceived, "A", "Body", DateTime.UtcNow));
        repository.Notifications.Add(new NotificationEntity(OtherUserId, NotificationType.MessageReceived, "B", "Body", DateTime.UtcNow));
        var service = CreateService(notificationRepository: repository);

        var response = await service.GetCurrentUserNotificationsAsync(CancellationToken.None);

        Assert.Single(response.Items);
        Assert.Equal(UserId, response.Items[0].RecipientUserId);
        Assert.Equal(1, response.UnreadCount);
    }

    [Fact]
    public async Task MarkAsReadAsync_RejectsForeignNotification()
    {
        var repository = new FakeNotificationRepository();
        var notification = new NotificationEntity(OtherUserId, NotificationType.MessageReceived, "A", "Body", DateTime.UtcNow);
        repository.Notifications.Add(notification);
        var service = CreateService(notificationRepository: repository);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            service.MarkAsReadAsync(notification.Id, CancellationToken.None));

        Assert.Equal(403, exception.StatusCode);
    }

    private static NotificationAppService CreateService(
        FakeNotificationRepository? notificationRepository = null,
        FakeNotificationEventRepository? eventRepository = null)
    {
        return new NotificationAppService(
            notificationRepository ?? new FakeNotificationRepository(),
            eventRepository ?? new FakeNotificationEventRepository(),
            new FakeCurrentUserContext(UserId));
    }

    private sealed class FakeCurrentUserContext : ICurrentUserContext
    {
        public FakeCurrentUserContext(Guid userId)
        {
            UserId = userId;
        }

        public Guid UserId { get; }
    }

    private sealed class FakeNotificationRepository : INotificationRepository
    {
        public List<NotificationEntity> Notifications { get; } = [];

        public Task AddAsync(NotificationEntity notification, CancellationToken cancellationToken)
        {
            Notifications.Add(notification);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<NotificationEntity>> GetByRecipientAsync(Guid recipientUserId, CancellationToken cancellationToken)
        {
            IReadOnlyCollection<NotificationEntity> result = Notifications
                .Where(x => x.RecipientUserId == recipientUserId)
                .ToArray();

            return Task.FromResult(result);
        }

        public Task<NotificationEntity?> GetByIdAsync(Guid notificationId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Notifications.FirstOrDefault(x => x.Id == notificationId));
        }

        public Task<int> CountUnreadAsync(Guid recipientUserId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Notifications.Count(x => x.RecipientUserId == recipientUserId && !x.IsRead));
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeNotificationEventRepository : INotificationEventRepository
    {
        public List<NotificationEvent> Events { get; } = [];

        public Task AddAsync(NotificationEvent notificationEvent, CancellationToken cancellationToken)
        {
            Events.Add(notificationEvent);
            return Task.CompletedTask;
        }

        public Task<NotificationEvent?> TryTakeNextAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<NotificationEvent?>(Events.FirstOrDefault());
        }

        public Task SaveAsync(NotificationEvent notificationEvent, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
