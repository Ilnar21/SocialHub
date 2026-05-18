using SocialHub.Message.Application.Abstractions;
using SocialHub.Message.Application.Exceptions;
using SocialHub.Message.Application.Models.Dialogs;
using SocialHub.Message.Application.Models.External;
using SocialHub.Message.Domain.Entities;

namespace SocialHub.Message.Application.Services;

public sealed class MessageService : IMessageService
{
    private readonly IDialogRepository _repository;
    private readonly ICurrentUserContext _currentUser;
    private readonly INotificationClient _notificationClient;

    public MessageService(
        IDialogRepository repository,
        ICurrentUserContext currentUser,
        INotificationClient notificationClient)
    {
        _repository = repository;
        _currentUser = currentUser;
        _notificationClient = notificationClient;
    }

    public async Task<SendMessageResponse> SendMessageAsync(string recipientUserId, SendMessageRequest request, CancellationToken cancellationToken)
    {
        var senderUserId = _currentUser.UserId;
        var normalizedRecipientUserId = recipientUserId.Trim();

        if (string.IsNullOrWhiteSpace(normalizedRecipientUserId) || normalizedRecipientUserId.Equals(senderUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw AppException.BadRequest("Получатель сообщения указан некорректно.");
        }

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            throw AppException.BadRequest("Текст сообщения не может быть пустым.");
        }

        var now = DateTimeOffset.UtcNow;
        var participants = NormalizeParticipants(senderUserId, normalizedRecipientUserId);
        var message = new DialogMessage(
            Guid.NewGuid().ToString("N"),
            senderUserId,
            normalizedRecipientUserId,
            request.Text.Trim(),
            now);

        var dialogId = BuildDialogId(participants[0], participants[1]);
        await _repository.SaveMessageAsync(dialogId, participants, message, cancellationToken);

        await _notificationClient.NotifyMessageReceivedAsync(
            new MessageReceivedNotification(normalizedRecipientUserId, senderUserId, message.Id, message.SentAt),
            cancellationToken);

        return new SendMessageResponse(dialogId, ToMessageResponse(message));
    }

    public async Task<IReadOnlyCollection<DialogSummaryResponse>> GetDialogsAsync(CancellationToken cancellationToken)
    {
        var dialogs = await _repository.GetDialogsForUserAsync(_currentUser.UserId, cancellationToken);
        return dialogs
            .OrderByDescending(x => x.LastMessageAt)
            .Select(x => new DialogSummaryResponse(x.Id, x.ParticipantUserIds, x.LastMessageAt, x.LastMessagePreview))
            .ToArray();
    }

    public async Task<MessagesResponse> GetMessagesAsync(string dialogId, int? skip, int? limit, CancellationToken cancellationToken)
    {
        var dialog = await _repository.GetDialogAsync(dialogId, cancellationToken)
            ?? throw AppException.NotFound("Диалог не найден.");

        if (!dialog.HasParticipant(_currentUser.UserId))
        {
            throw AppException.Forbidden("Доступ запрещён.");
        }

        var safeSkip = Math.Max(skip ?? 0, 0);
        var safeLimit = Math.Clamp(limit ?? 50, 1, 100);
        var messages = dialog.Messages
            .OrderBy(x => x.SentAt)
            .Skip(safeSkip)
            .Take(safeLimit)
            .Select(ToMessageResponse)
            .ToArray();

        return new MessagesResponse(dialog.Id, messages);
    }

    private static string[] NormalizeParticipants(string first, string second)
    {
        return new[] { first.Trim(), second.Trim() }
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string BuildDialogId(string first, string second)
    {
        static string Clean(string value)
        {
            var chars = value.Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_').ToArray();
            return new string(chars).Trim('_');
        }

        return $"dialog_{Clean(first)}_{Clean(second)}";
    }

    private static MessageResponse ToMessageResponse(DialogMessage message)
    {
        return new MessageResponse(
            message.Id,
            message.SenderUserId,
            message.RecipientUserId,
            message.Text,
            message.SentAt);
    }
}
