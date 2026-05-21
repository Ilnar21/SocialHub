namespace SocialHub.Post.Domain;

public sealed class PostContent
{
    public Guid PostId { get; init; }
    public string Text { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public PostContent(Guid postId, string text, DateTimeOffset updatedAt)
    {
        if (postId == Guid.Empty)
        {
            throw new DomainException("Post id is required.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new DomainException("Text is required.");
        }

        PostId = postId;
        Text = text.Trim();
        UpdatedAt = updatedAt;
    }

    public void ChangeText(string text, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new DomainException("Text is required.");
        }

        Text = text.Trim();
        UpdatedAt = now;
    }
}
