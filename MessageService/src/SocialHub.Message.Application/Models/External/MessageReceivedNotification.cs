namespace SocialHub.Message.Application.Models.External;

public sealed record MessageReceivedNotification(
    string RecipientUserId,
    string SenderUserId,
    string MessageId,
    DateTimeOffset SentAt);
