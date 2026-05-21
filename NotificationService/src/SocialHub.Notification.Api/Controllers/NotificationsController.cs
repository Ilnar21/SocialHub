using Microsoft.AspNetCore.Mvc;
using SocialHub.Notification.Application.Abstractions;
using SocialHub.Notification.Application.Models.Notifications;

namespace SocialHub.Notification.Api.Controllers;

[ApiController]
[Route("api/notifications")]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpPost("internal")]
    public async Task<ActionResult<NotificationResponse>> CreateInternalNotification(
        CreateNotificationRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _notificationService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetNotifications), new { notificationId = response.Id }, response);
    }

    [HttpGet]
    public async Task<ActionResult<NotificationListResponse>> GetNotifications(CancellationToken cancellationToken)
    {
        return Ok(await _notificationService.GetCurrentUserNotificationsAsync(cancellationToken));
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountResponse>> GetUnreadCount(CancellationToken cancellationToken)
    {
        return Ok(await _notificationService.GetCurrentUserUnreadCountAsync(cancellationToken));
    }

    [HttpPatch("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid notificationId, CancellationToken cancellationToken)
    {
        await _notificationService.MarkAsReadAsync(notificationId, cancellationToken);
        return NoContent();
    }
}
