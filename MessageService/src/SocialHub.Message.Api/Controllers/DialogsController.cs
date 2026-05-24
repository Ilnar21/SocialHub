using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialHub.Message.Application.Abstractions;
using SocialHub.Message.Application.Models.Dialogs;

namespace SocialHub.Message.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dialogs")]
public sealed class DialogsController : ControllerBase
{
    private readonly IMessageService _messageService;

    public DialogsController(IMessageService messageService)
    {
        _messageService = messageService;
    }

    [HttpPost("{recipientUserId}/messages")]
    public async Task<ActionResult<SendMessageResponse>> SendMessage(
        string recipientUserId,
        SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _messageService.SendMessageAsync(recipientUserId, request, cancellationToken);
        return Created($"/api/dialogs/{response.DialogId}/messages/{response.Message.Id}", response);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<DialogSummaryResponse>>> GetDialogs(CancellationToken cancellationToken)
    {
        return Ok(await _messageService.GetDialogsAsync(cancellationToken));
    }

    [HttpGet("{dialogId}/messages")]
    public async Task<ActionResult<MessagesResponse>> GetMessages(
        string dialogId,
        [FromQuery] int? skip,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        return Ok(await _messageService.GetMessagesAsync(dialogId, skip, limit, cancellationToken));
    }
}
