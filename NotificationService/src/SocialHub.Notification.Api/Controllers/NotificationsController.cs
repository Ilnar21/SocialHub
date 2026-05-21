using Microsoft.AspNetCore.Mvc;
using SocialHub.Notification.Application.Abstractions;
using SocialHub.Notification.Application.Models.Notifications;

namespace SocialHub.Notification.Api.Controllers;

[ApiController]
[Route("api/notifications")]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(INotificationService notificationService, ILogger<NotificationsController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    [HttpPost("internal")]
    public async Task<ActionResult<NotificationResponse>> CreateInternalNotification(
        CreateNotificationRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _notificationService.CreateAsync(request, cancellationToken);
        _logger.LogInformation("Internal notification {NotificationId} created for user {UserId}.", response.Id, response.RecipientUserId);
        return CreatedAtAction(nameof(GetNotifications), new { notificationId = response.Id }, response);
    }

    [HttpGet]
    public async Task<ActionResult<NotificationListResponse>> GetNotifications(CancellationToken cancellationToken)
    {
        var response = await _notificationService.GetCurrentUserNotificationsAsync(cancellationToken);
        _logger.LogInformation("Notification inbox requested. Returned {Count} items.", response.Items.Count);
        return Ok(response);
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountResponse>> GetUnreadCount(CancellationToken cancellationToken)
    {
        var response = await _notificationService.GetCurrentUserUnreadCountAsync(cancellationToken);
        _logger.LogInformation("Unread notification count requested. Count is {UnreadCount}.", response.Count);
        return Ok(response);
    }

    [HttpPatch("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid notificationId, CancellationToken cancellationToken)
    {
        await _notificationService.MarkAsReadAsync(notificationId, cancellationToken);
        _logger.LogInformation("Notification {NotificationId} marked as read.", notificationId);
        return NoContent();
    }
}
