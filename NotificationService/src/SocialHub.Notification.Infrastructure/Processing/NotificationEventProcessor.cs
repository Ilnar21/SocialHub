using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SocialHub.Notification.Application.Abstractions;
using NotificationEntity = SocialHub.Notification.Domain.Entities.Notification;

namespace SocialHub.Notification.Infrastructure.Processing;

public sealed class NotificationEventProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationEventProcessor> _logger;

    public NotificationEventProcessor(IServiceScopeFactory scopeFactory, ILogger<NotificationEventProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessNextEventAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    private async Task ProcessNextEventAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var events = scope.ServiceProvider.GetRequiredService<INotificationEventRepository>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationRepository>();

        var notificationEvent = await events.TryTakeNextAsync(cancellationToken);
        if (notificationEvent is null)
        {
            return;
        }

        try
        {
            var notification = new NotificationEntity(
                notificationEvent.RecipientUserId,
                notificationEvent.Type,
                notificationEvent.Title,
                notificationEvent.Message,
                DateTime.UtcNow,
                notificationEvent.SourceEntityId,
                notificationEvent.SourceService);

            await notifications.AddAsync(notification, cancellationToken);
            await notifications.SaveChangesAsync(cancellationToken);

            notificationEvent.MarkCompleted(DateTime.UtcNow);
            await events.SaveAsync(notificationEvent, cancellationToken);

            _logger.LogInformation("Notification event {EventId} processed into notification {NotificationId}.", notificationEvent.Id, notification.Id);
        }
        catch (Exception ex)
        {
            notificationEvent.MarkFailed(ex.Message);
            await events.SaveAsync(notificationEvent, cancellationToken);
            _logger.LogWarning(ex, "Notification event {EventId} failed.", notificationEvent.Id);
        }
    }
}
