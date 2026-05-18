using SocialHub.Message.Domain.Entities;

namespace SocialHub.Message.Application.Abstractions;

public interface IDialogRepository
{
    Task SaveMessageAsync(string dialogId, IReadOnlyCollection<string> participantUserIds, DialogMessage message, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Dialog>> GetDialogsForUserAsync(string userId, CancellationToken cancellationToken);
    Task<Dialog?> GetDialogAsync(string dialogId, CancellationToken cancellationToken);
}
