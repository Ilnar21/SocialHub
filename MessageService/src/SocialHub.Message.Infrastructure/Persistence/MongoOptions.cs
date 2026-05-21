namespace SocialHub.Message.Infrastructure.Persistence;

public sealed record MongoOptions
{
    public string ConnectionString { get; init; } = "mongodb://localhost:27017";
    public string DatabaseName { get; init; } = "socialhub_messages";
    public string DialogsCollection { get; init; } = "dialogs";
}
