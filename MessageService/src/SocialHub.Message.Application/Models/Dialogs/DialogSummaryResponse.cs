namespace SocialHub.Message.Application.Models.Dialogs;

public sealed record DialogSummaryResponse(
    string DialogId,
    IReadOnlyCollection<string> ParticipantUserIds,
    DateTimeOffset LastMessageAt,
    string LastMessagePreview);
