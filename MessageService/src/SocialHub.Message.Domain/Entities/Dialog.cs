namespace SocialHub.Message.Domain.Entities;

public sealed class Dialog
{
    public Dialog(
        string id,
        IReadOnlyCollection<string> participantUserIds,
        DateTimeOffset lastMessageAt,
        string lastMessagePreview,
        IReadOnlyCollection<DialogMessage> messages)
    {
        Id = id;
        ParticipantUserIds = participantUserIds.ToArray();
        LastMessageAt = lastMessageAt;
        LastMessagePreview = lastMessagePreview;
        Messages = messages.ToArray();
    }

    public string Id { get; }
    public IReadOnlyCollection<string> ParticipantUserIds { get; }
    public DateTimeOffset LastMessageAt { get; }
    public string LastMessagePreview { get; }
    public IReadOnlyCollection<DialogMessage> Messages { get; }

    public bool HasParticipant(string userId)
    {
        return ParticipantUserIds.Contains(userId, StringComparer.OrdinalIgnoreCase);
    }
}
