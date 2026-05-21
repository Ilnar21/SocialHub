using SocialHub.Notification.Application.Abstractions;
using SocialHub.Notification.Application.Exceptions;
using SocialHub.Notification.Application.Models.Notifications;
using NotificationEntity = SocialHub.Notification.Domain.Entities.Notification;

namespace SocialHub.Notification.Application.Services;

public sealed class NotificationService : INotificationService
{
    private readonly INotificationRepository _repository;
    private readonly ICurrentUserContext _currentUser;

    public NotificationService(INotificationRepository repository, ICurrentUserContext currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<NotificationResponse> CreateAsync(CreateNotificationRequest request, CancellationToken cancellationToken)
    {
        var notification = new NotificationEntity(
            request.RecipientUserId,
            request.Type,
            request.Title,
            request.Message,
            DateTime.UtcNow);

        await _repository.AddAsync(notification, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return ToResponse(notification);
    }

    public async Task<NotificationListResponse> GetCurrentUserNotificationsAsync(CancellationToken cancellationToken)
    {
        var notifications = await _repository.GetByRecipientAsync(_currentUser.UserId, cancellationToken);
        var unreadCount = notifications.Count(x => !x.IsRead);

        return new NotificationListResponse(
            notifications.Select(ToResponse).ToArray(),
            unreadCount);
    }

    public async Task<UnreadCountResponse> GetCurrentUserUnreadCountAsync(CancellationToken cancellationToken)
    {
        var count = await _repository.CountUnreadAsync(_currentUser.UserId, cancellationToken);
        return new UnreadCountResponse(count);
    }

    public async Task MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken)
    {
        var notification = await _repository.GetByIdAsync(notificationId, cancellationToken)
            ?? throw AppException.Unauthorized("Notification was not found.");

        if (notification.RecipientUserId != _currentUser.UserId)
        {
            throw AppException.Unauthorized("Current user cannot read this notification.");
        }

        notification.MarkAsRead(DateTime.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);
    }

    private static NotificationResponse ToResponse(NotificationEntity notification)
    {
        return new NotificationResponse(
            notification.Id,
            notification.RecipientUserId,
            notification.Type,
            notification.Title,
            notification.Message,
            notification.IsRead,
            notification.CreatedAtUtc,
            notification.ReadAtUtc);
    }
}
