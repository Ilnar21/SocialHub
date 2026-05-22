using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SocialHub.Post.Infrastructure.Persistence;

public sealed class PostContentDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid PostId { get; set; }

    public string Text { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }
}
