using MongoDB.Driver;
using SocialHub.Message.Application.Abstractions;
using SocialHub.Message.Domain.Entities;

namespace SocialHub.Message.Infrastructure.Persistence;

public sealed class MongoDialogRepository : IDialogRepository
{
    private readonly IMongoCollection<DialogDocument> _dialogs;

    public MongoDialogRepository(IMongoDatabase database, MongoOptions options)
    {
        _dialogs = database.GetCollection<DialogDocument>(options.DialogsCollection);
    }

    public async Task SaveMessageAsync(string dialogId, IReadOnlyCollection<string> participantUserIds, DialogMessage message, CancellationToken cancellationToken)
    {
        var document = ToDocument(message);
        var filter = Builders<DialogDocument>.Filter.Eq(x => x.Id, dialogId);
        var update = Builders<DialogDocument>.Update
            .SetOnInsert(x => x.Id, dialogId)
            .SetOnInsert(x => x.ParticipantUserIds, participantUserIds.ToArray())
            .Set(x => x.LastMessageAt, message.SentAt)
            .Set(x => x.LastMessagePreview, message.Text)
            .Push(x => x.Messages, document);

        await _dialogs.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Dialog>> GetDialogsForUserAsync(string userId, CancellationToken cancellationToken)
    {
        var filter = Builders<DialogDocument>.Filter.AnyEq(x => x.ParticipantUserIds, userId);
        var documents = await _dialogs.Find(filter)
            .SortByDescending(x => x.LastMessageAt)
            .ToListAsync(cancellationToken);

        return documents.Select(ToDomain).ToArray();
    }

    public async Task<Dialog?> GetDialogAsync(string dialogId, CancellationToken cancellationToken)
    {
        var document = await _dialogs.Find(x => x.Id == dialogId).FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : ToDomain(document);
    }

    private static Dialog ToDomain(DialogDocument document)
    {
        return new Dialog(
            document.Id,
            document.ParticipantUserIds,
            document.LastMessageAt,
            document.LastMessagePreview,
            document.Messages.Select(ToDomain).ToArray());
    }

    private static DialogMessage ToDomain(MessageDocument document)
    {
        return new DialogMessage(
            document.Id,
            document.SenderUserId,
            document.RecipientUserId,
            document.Text,
            document.SentAt);
    }

    private static MessageDocument ToDocument(DialogMessage message)
    {
        return new MessageDocument(
            message.Id,
            message.SenderUserId,
            message.RecipientUserId,
            message.Text,
            message.SentAt);
    }
}
