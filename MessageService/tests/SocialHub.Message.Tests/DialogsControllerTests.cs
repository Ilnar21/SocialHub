using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using SocialHub.Message.Api.Controllers;
using SocialHub.Message.Application.Abstractions;
using SocialHub.Message.Application.Models.Dialogs;

namespace SocialHub.Message.Tests;

[TestFixture]
public sealed class DialogsControllerTests
{
    [Test]
    public async Task SendMessage_returns_created_response()
    {
        var controller = new DialogsController(new FakeMessageService());

        var result = await controller.SendMessage("maria.sokolova", new SendMessageRequest("Привет"), CancellationToken.None);

        Assert.That(result.Result, Is.TypeOf<CreatedResult>());
    }

    [Test]
    public async Task GetDialogs_returns_ok_response()
    {
        var controller = new DialogsController(new FakeMessageService());

        var result = await controller.GetDialogs(CancellationToken.None);

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
    }

    [Test]
    public async Task GetMessages_returns_ok_response()
    {
        var controller = new DialogsController(new FakeMessageService());

        var result = await controller.GetMessages("dialog_ivan_maria", null, null, CancellationToken.None);

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
    }

    [Test]
    public void DialogsController_requires_jwt_authorization()
    {
        var authorize = Attribute.GetCustomAttribute(typeof(DialogsController), typeof(AuthorizeAttribute)) as AuthorizeAttribute;

        Assert.That(authorize, Is.Not.Null);
        Assert.That(authorize!.Roles, Is.Null);
    }

    private sealed class FakeMessageService : IMessageService
    {
        public Task<SendMessageResponse> SendMessageAsync(string recipientUserId, SendMessageRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new SendMessageResponse(
                "dialog_ivan_maria",
                new MessageResponse("message-1", "ivan.petrov", recipientUserId, request.Text, DateTimeOffset.UtcNow)));
        }

        public Task<IReadOnlyCollection<DialogSummaryResponse>> GetDialogsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<DialogSummaryResponse>>(new[]
            {
                new DialogSummaryResponse("dialog_ivan_maria", new[] { "ivan.petrov", "maria.sokolova" }, DateTimeOffset.UtcNow, "Привет")
            });
        }

        public Task<MessagesResponse> GetMessagesAsync(string dialogId, int? skip, int? limit, CancellationToken cancellationToken)
        {
            return Task.FromResult(new MessagesResponse(dialogId, new[]
            {
                new MessageResponse("message-1", "ivan.petrov", "maria.sokolova", "Привет", DateTimeOffset.UtcNow)
            }));
        }
    }
}
