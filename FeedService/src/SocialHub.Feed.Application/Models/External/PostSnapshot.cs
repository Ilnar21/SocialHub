namespace SocialHub.Feed.Application.Models.External;

/// <summary>
/// «Снимок» поста, который Feed Service получает от Post Service.
/// Минимальный набор полей, нужный для ранжирования и отображения карточки.
/// </summary>
public sealed record PostSnapshot(
    Guid Id,
    Guid CommunityId,
    Guid AuthorId,
    string Title,
    string PreviewText,
    int Likes,
    int Comments,
    DateTimeOffset CreatedAt);
