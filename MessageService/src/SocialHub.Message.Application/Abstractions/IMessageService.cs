using SocialHub.Message.Application.Models.Dialogs;

namespace SocialHub.Message.Application.Abstractions;

public interface IMessageService
{
    Task<SendMessageResponse> SendMessageAsync(string recipientUserId, SendMessageRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<DialogSummaryResponse>> GetDialogsAsync(CancellationToken cancellationToken);
    Task<MessagesResponse> GetMessagesAsync(string dialogId, int? skip, int? limit, CancellationToken cancellationToken);
}
