using Microsoft.AspNetCore.Mvc;
using SocialHub.Notification.Domain.Enums;

namespace SocialHub.Notification.Api.Controllers;

[ApiController]
[Route("api/notifications")]
public sealed class NotificationsController : ControllerBase
{
    [HttpGet("preview")]
    public ActionResult<object> Preview()
    {
        return Ok(new
        {
            service = "notification-service",
            storage = "not-configured-yet",
            supportedTypes = Enum.GetNames<NotificationType>()
        });
    }
}
