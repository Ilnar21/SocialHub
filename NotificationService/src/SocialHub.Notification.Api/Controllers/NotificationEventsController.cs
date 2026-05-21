using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialHub.Notification.Api.Security;
using SocialHub.Notification.Application.Abstractions;
using SocialHub.Notification.Application.Models.Events;

namespace SocialHub.Notification.Api.Controllers;

[ApiController]
[AllowAnonymous]
[ServiceFilter(typeof(InternalTokenFilter))]
[Route("api/notification-events")]
public sealed class NotificationEventsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationEventsController> _logger;

    public NotificationEventsController(INotificationService notificationService, ILogger<NotificationEventsController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<NotificationEventResponse>> CreateEvent(
        CreateNotificationEventRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _notificationService.CreateEventAsync(request, cancellationToken);
        _logger.LogInformation(
            "Notification event {EventId} accepted from {SourceService} for user {UserId}.",
            response.Id,
            response.SourceService,
            response.RecipientUserId);
        return Accepted(response);
    }
}
