namespace SocialHub.Message.Application.Models.Dialogs;

public sealed record MessageResponse(
    string Id,
    string SenderUserId,
    string RecipientUserId,
    string Text,
    DateTimeOffset SentAt);
