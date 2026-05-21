using Microsoft.AspNetCore.Mvc;

namespace SocialHub.Moderation.Api.Controllers;

[ApiController]
[Route("api/private-messages")]
public sealed class PrivateMessagesController : ControllerBase
{
    [HttpGet("{**_}")]
    public IActionResult DenyPrivateMessages()
    {
        return Problem("Доступ к личным сообщениям запрещён", statusCode: StatusCodes.Status403Forbidden);
    }
}
