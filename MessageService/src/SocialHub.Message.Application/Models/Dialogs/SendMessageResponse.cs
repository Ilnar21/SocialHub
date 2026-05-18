namespace SocialHub.Message.Application.Models.Dialogs;

public sealed record SendMessageResponse(string DialogId, MessageResponse Message);
