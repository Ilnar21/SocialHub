namespace SocialHub.Message.Domain.Entities;

public sealed class DialogMessage
{
    public DialogMessage(
        string id,
        string senderUserId,
        string recipientUserId,
        string text,
        DateTimeOffset sentAt)
    {
        Id = id;
        SenderUserId = senderUserId;
        RecipientUserId = recipientUserId;
        Text = text;
        SentAt = sentAt;
    }

    public string Id { get; }
    public string SenderUserId { get; }
    public string RecipientUserId { get; }
    public string Text { get; }
    public DateTimeOffset SentAt { get; }
}
