namespace SocialHub.Message.Infrastructure.Persistence;

public sealed record MessageDocument(
    string Id,
    string SenderUserId,
    string RecipientUserId,
    string Text,
    DateTimeOffset SentAt);
