using Microsoft.AspNetCore.Mvc;
using SocialHub.Notification.Application.Abstractions;
using SocialHub.Notification.Application.Models.Events;

namespace SocialHub.Notification.Api.Controllers;

[ApiController]
[Route("api/notification-events")]
public sealed class NotificationEventsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationEventsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpPost]
    public async Task<ActionResult<NotificationEventResponse>> CreateEvent(
        CreateNotificationEventRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _notificationService.CreateEventAsync(request, cancellationToken);
        return Accepted(response);
    }
}
