using MongoDB.Bson;
using MongoDB.Driver;
using NUnit.Framework;
using SocialHub.Message.Domain.Entities;
using SocialHub.Message.Infrastructure.Persistence;

namespace SocialHub.Message.Tests;

[TestFixture]
public sealed class MongoDialogRepositoryTests
{
    [Test]
    public async Task Repository_preserves_history_between_repository_instances()
    {
        var connectionString = Environment.GetEnvironmentVariable("MESSAGE_TEST_MONGO") ?? "mongodb://localhost:27017";
        var client = new MongoClient(connectionString);

        try
        {
            await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        }
        catch
        {
            Assert.Ignore("MongoDB is not available. Set MESSAGE_TEST_MONGO or run MessageService/docker-compose.yml.");
        }

        var databaseName = "socialhub_message_tests_" + Guid.NewGuid().ToString("N");
        var options = new MongoOptions { ConnectionString = connectionString, DatabaseName = databaseName, DialogsCollection = "dialogs" };
        var database = client.GetDatabase(databaseName);

        try
        {
            var firstRepository = new MongoDialogRepository(database, options);
            await firstRepository.SaveMessageAsync(
                "dialog_ivan_maria",
                new[] { "ivan.petrov", "maria.sokolova" },
                new DialogMessage("message-1", "ivan.petrov", "maria.sokolova", "После рестарта всё на месте", DateTimeOffset.UtcNow),
                CancellationToken.None);

            var secondRepository = new MongoDialogRepository(database, options);
            var dialog = await secondRepository.GetDialogAsync("dialog_ivan_maria", CancellationToken.None);

            Assert.That(dialog, Is.Not.Null);
            Assert.That(dialog!.Messages.Single().Text, Is.EqualTo("После рестарта всё на месте"));
        }
        finally
        {
            await client.DropDatabaseAsync(databaseName);
        }
    }
}
