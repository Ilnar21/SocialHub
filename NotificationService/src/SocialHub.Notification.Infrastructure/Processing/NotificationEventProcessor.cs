using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocialHub.Notification.Application.Abstractions;
using SocialHub.Notification.Application.Models.Email;
using NotificationEntity = SocialHub.Notification.Domain.Entities.Notification;

namespace SocialHub.Notification.Infrastructure.Processing;

public sealed class NotificationEventProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationEventProcessor> _logger;
    private readonly NotificationProcessingOptions _options;

    public NotificationEventProcessor(
        IServiceScopeFactory scopeFactory,
        IOptions<NotificationProcessingOptions> options,
        ILogger<NotificationEventProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNextEventAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Notification event processor cycle failed. Worker will continue after delay.");
            }

            await DelayBeforeNextCycleAsync(stoppingToken);
        }
    }

    private async Task ProcessNextEventAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var events = scope.ServiceProvider.GetRequiredService<INotificationEventRepository>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

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

            await TrySendEmailAsync(emailSender, notificationEvent, cancellationToken);

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

    private async Task TrySendEmailAsync(
        IEmailSender emailSender,
        Domain.Entities.NotificationEvent notificationEvent,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await emailSender.SendAsync(
                new EmailMessage(notificationEvent.RecipientEmail, notificationEvent.Title, notificationEvent.Message),
                cancellationToken);

            if (result.Skipped)
            {
                _logger.LogInformation(
                    "Email delivery for notification event {EventId} was skipped: {Reason}.",
                    notificationEvent.Id,
                    result.Details);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Email delivery for notification event {EventId} failed. In-site notification remains available.",
                notificationEvent.Id);
        }
    }

    private Task DelayBeforeNextCycleAsync(CancellationToken stoppingToken)
    {
        var delaySeconds = Math.Max(1, _options.PollingIntervalSeconds);
        return Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
    }
}
