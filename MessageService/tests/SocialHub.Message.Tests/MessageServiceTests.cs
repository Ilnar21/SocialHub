using NUnit.Framework;
using SocialHub.Message.Application.Abstractions;
using SocialHub.Message.Application.Exceptions;
using SocialHub.Message.Application.Models.Dialogs;
using SocialHub.Message.Application.Models.External;
using SocialHub.Message.Domain.Constants;
using SocialHub.Message.Domain.Entities;
using MessageAppService = SocialHub.Message.Application.Services.MessageService;

namespace SocialHub.Message.Tests;

[TestFixture]
public sealed class MessageServiceTests
{
    [Test]
    public async Task SendMessageAsync_saves_message_and_returns_dialog_id()
    {
        var repository = new InMemoryDialogRepository();
        var service = CreateService(repository, currentUserId: "ivan.petrov");

        var response = await service.SendMessageAsync("maria.sokolova", new SendMessageRequest("Привет"), CancellationToken.None);

        Assert.That(response.DialogId, Is.EqualTo("dialog_ivan_petrov_maria_sokolova"));
        Assert.That(response.Message.Text, Is.EqualTo("Привет"));
        Assert.That(repository.SavedMessages, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task GetDialogsAsync_returns_dialogs_for_current_user()
    {
        var repository = new InMemoryDialogRepository();
        repository.Dialogs["dialog_ivan_maria"] = new Dialog(
            "dialog_ivan_maria",
            new[] { "ivan.petrov", "maria.sokolova" },
            DateTimeOffset.UtcNow,
            "Последнее",
            Array.Empty<DialogMessage>());

        var service = CreateService(repository, currentUserId: "ivan.petrov");
        var dialogs = await service.GetDialogsAsync(CancellationToken.None);

        Assert.That(dialogs, Has.Count.EqualTo(1));
        Assert.That(dialogs.Single().LastMessagePreview, Is.EqualTo("Последнее"));
    }

    [Test]
    public async Task GetMessagesAsync_returns_history_in_chronological_order()
    {
        var repository = new InMemoryDialogRepository();
        repository.Dialogs["dialog_ivan_maria"] = new Dialog(
            "dialog_ivan_maria",
            new[] { "ivan.petrov", "maria.sokolova" },
            DateTimeOffset.UtcNow,
            "",
            new[]
            {
                new DialogMessage("2", "maria.sokolova", "ivan.petrov", "Второе", DateTimeOffset.UtcNow.AddMinutes(2)),
                new DialogMessage("1", "ivan.petrov", "maria.sokolova", "Первое", DateTimeOffset.UtcNow.AddMinutes(1))
            });

        var service = CreateService(repository, currentUserId: "ivan.petrov");
        var response = await service.GetMessagesAsync("dialog_ivan_maria", null, null, CancellationToken.None);

        Assert.That(response.Messages.Select(x => x.Text), Is.EqualTo(new[] { "Первое", "Второе" }));
    }

    [Test]
    public void GetMessagesAsync_rejects_foreign_dialog()
    {
        var repository = new InMemoryDialogRepository();
        repository.Dialogs["dialog_ivan_maria"] = new Dialog(
            "dialog_ivan_maria",
            new[] { "ivan.petrov", "maria.sokolova" },
            DateTimeOffset.UtcNow,
            "",
            Array.Empty<DialogMessage>());

        var service = CreateService(repository, currentUserId: "alexey");

        var ex = Assert.ThrowsAsync<AppException>(() => service.GetMessagesAsync("dialog_ivan_maria", null, null, CancellationToken.None));
        Assert.That(ex!.StatusCode, Is.EqualTo(403));
    }

    [Test]
    public void SendMessageAsync_rejects_too_long_message()
    {
        var service = CreateService(new InMemoryDialogRepository(), currentUserId: "ivan.petrov");
        var text = new string('a', MessageLimits.MaxMessageTextLength + 1);

        var ex = Assert.ThrowsAsync<AppException>(() => service.SendMessageAsync("maria.sokolova", new SendMessageRequest(text), CancellationToken.None));
        Assert.That(ex!.StatusCode, Is.EqualTo(400));
    }

    [Test]
    public async Task SendMessageAsync_does_not_wait_for_notification_service()
    {
        var notification = new NeverCompletingNotificationClient();
        var service = CreateService(new InMemoryDialogRepository(), currentUserId: "ivan.petrov", notification);

        var sendTask = service.SendMessageAsync("maria.sokolova", new SendMessageRequest("Не ждём уведомление"), CancellationToken.None);
        var completed = await Task.WhenAny(sendTask, Task.Delay(TimeSpan.FromMilliseconds(300)));

        Assert.That(completed, Is.EqualTo(sendTask));
    }

    private static MessageAppService CreateService(
        InMemoryDialogRepository repository,
        string currentUserId,
        INotificationClient? notificationClient = null)
    {
        return new MessageAppService(
            repository,
            new TestCurrentUserContext(currentUserId),
            notificationClient ?? new NoopNotificationClient());
    }

    private sealed record SavedMessage(string DialogId, IReadOnlyCollection<string> ParticipantUserIds, DialogMessage Message);

    private sealed class InMemoryDialogRepository : IDialogRepository
    {
        public Dictionary<string, Dialog> Dialogs { get; } = new();
        public List<SavedMessage> SavedMessages { get; } = new();

        public Task SaveMessageAsync(string dialogId, IReadOnlyCollection<string> participantUserIds, DialogMessage message, CancellationToken cancellationToken)
        {
            SavedMessages.Add(new SavedMessage(dialogId, participantUserIds, message));
            Dialogs[dialogId] = new Dialog(dialogId, participantUserIds, message.SentAt, message.Text, new[] { message });
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<Dialog>> GetDialogsForUserAsync(string userId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<Dialog>>(Dialogs.Values.Where(x => x.HasParticipant(userId)).ToArray());
        }

        public Task<Dialog?> GetDialogAsync(string dialogId, CancellationToken cancellationToken)
        {
            Dialogs.TryGetValue(dialogId, out var dialog);
            return Task.FromResult(dialog);
        }
    }

    private sealed class TestCurrentUserContext : ICurrentUserContext
    {
        public TestCurrentUserContext(string userId) => UserId = userId;
        public string UserId { get; }
    }

    private sealed class NoopNotificationClient : INotificationClient
    {
        public Task NotifyMessageReceivedAsync(MessageReceivedNotification request, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class NeverCompletingNotificationClient : INotificationClient
    {
        public Task NotifyMessageReceivedAsync(MessageReceivedNotification request, CancellationToken cancellationToken) => Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }
}
