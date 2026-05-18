using MongoDB.Bson.Serialization.Attributes;

namespace SocialHub.Message.Infrastructure.Persistence;

public sealed record DialogDocument
{
    [BsonId]
    public string Id { get; init; } = "";

    public string[] ParticipantUserIds { get; init; } = [];
    public DateTimeOffset LastMessageAt { get; init; }
    public string LastMessagePreview { get; init; } = "";
    public List<MessageDocument> Messages { get; init; } = [];
}
