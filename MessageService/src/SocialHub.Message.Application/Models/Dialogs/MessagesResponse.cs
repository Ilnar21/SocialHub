namespace SocialHub.Message.Application.Models.Dialogs;

public sealed record MessagesResponse(
    string DialogId,
    IReadOnlyCollection<MessageResponse> Messages);
