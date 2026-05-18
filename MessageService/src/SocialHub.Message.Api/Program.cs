using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MongoOptions>(builder.Configuration.GetSection("Mongo"));
builder.Services.AddSingleton(sp =>
{
    var options = builder.Configuration.GetSection("Mongo").Get<MongoOptions>() ?? new MongoOptions();
    var client = new MongoClient(options.ConnectionString);
    return client.GetDatabase(options.DatabaseName).GetCollection<DialogDocument>(options.DialogsCollection);
});
builder.Services.AddHttpClient("notifications", client =>
{
    var baseUrl = builder.Configuration["NotificationService:BaseUrl"];
    if (!string.IsNullOrWhiteSpace(baseUrl))
    {
        client.BaseAddress = new Uri(baseUrl);
    }
});

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "message-service" }));

app.MapPost("/api/dialogs/{recipientUserId}/messages", async (
    string recipientUserId,
    SendMessageRequest request,
    HttpContext http,
    IMongoCollection<DialogDocument> dialogs,
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    var senderUserId = GetUserId(http);
    if (senderUserId is null)
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(recipientUserId) || recipientUserId == senderUserId)
    {
        return Results.BadRequest(new ErrorResponse("recipient_invalid", "Получатель сообщения указан некорректно"));
    }

    if (string.IsNullOrWhiteSpace(request.Text))
    {
        return Results.BadRequest(new ErrorResponse("message_empty", "Текст сообщения не может быть пустым"));
    }

    var now = DateTimeOffset.UtcNow;
    var participants = NormalizeParticipants(senderUserId, recipientUserId);
    var message = new MessageDocument(
        ObjectId.GenerateNewId().ToString(),
        senderUserId,
        recipientUserId,
        request.Text.Trim(),
        now);

    var dialogId = BuildDialogId(participants[0], participants[1]);
    var filter = Builders<DialogDocument>.Filter.Eq(x => x.Id, dialogId);
    var update = Builders<DialogDocument>.Update
        .SetOnInsert(x => x.Id, dialogId)
        .SetOnInsert(x => x.ParticipantUserIds, participants)
        .Set(x => x.LastMessageAt, now)
        .Set(x => x.LastMessagePreview, message.Text)
        .Push(x => x.Messages, message);

    await dialogs.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken);

    _ = TryNotifyAsync(httpClientFactory, recipientUserId, senderUserId, message, loggerFactory.CreateLogger("Notification"), CancellationToken.None);

    return Results.Created($"/api/dialogs/{dialogId}/messages/{message.Id}", new SendMessageResponse(dialogId, message));
});

app.MapGet("/api/dialogs", async (
    HttpContext http,
    IMongoCollection<DialogDocument> dialogs,
    CancellationToken cancellationToken) =>
{
    var userId = GetUserId(http);
    if (userId is null)
    {
        return Results.Unauthorized();
    }

    var filter = Builders<DialogDocument>.Filter.AnyEq(x => x.ParticipantUserIds, userId);
    var result = await dialogs.Find(filter)
        .SortByDescending(x => x.LastMessageAt)
        .Project(x => new DialogSummaryResponse(
            x.Id,
            x.ParticipantUserIds,
            x.LastMessageAt,
            x.LastMessagePreview))
        .ToListAsync(cancellationToken);

    return Results.Ok(result);
});

app.MapGet("/api/dialogs/{dialogId}/messages", async (
    string dialogId,
    int? skip,
    int? limit,
    HttpContext http,
    IMongoCollection<DialogDocument> dialogs,
    CancellationToken cancellationToken) =>
{
    var userId = GetUserId(http);
    if (userId is null)
    {
        return Results.Unauthorized();
    }

    var dialog = await dialogs.Find(x => x.Id == dialogId).FirstOrDefaultAsync(cancellationToken);
    if (dialog is null)
    {
        return Results.NotFound(new ErrorResponse("dialog_not_found", "Диалог не найден"));
    }

    if (!dialog.ParticipantUserIds.Contains(userId, StringComparer.OrdinalIgnoreCase))
    {
        return Results.Forbid();
    }

    var safeSkip = Math.Max(skip ?? 0, 0);
    var safeLimit = Math.Clamp(limit ?? 50, 1, 100);
    var messages = dialog.Messages
        .OrderBy(x => x.SentAt)
        .Skip(safeSkip)
        .Take(safeLimit)
        .ToArray();

    return Results.Ok(new MessagesResponse(dialog.Id, messages));
});

app.Run();

static string? GetUserId(HttpContext http)
{
    var userId = http.Request.Headers["X-User-Id"].FirstOrDefault();
    return string.IsNullOrWhiteSpace(userId) ? null : userId.Trim();
}

static string[] NormalizeParticipants(string first, string second) =>
    new[] { first.Trim(), second.Trim() }.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();

static string BuildDialogId(string first, string second)
{
    static string Clean(string value)
    {
        var chars = value.Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_').ToArray();
        return new string(chars).Trim('_');
    }

    return $"dialog_{Clean(first)}_{Clean(second)}";
}

static async Task TryNotifyAsync(
    IHttpClientFactory httpClientFactory,
    string recipientUserId,
    string senderUserId,
    MessageDocument message,
    ILogger logger,
    CancellationToken cancellationToken)
{
    var client = httpClientFactory.CreateClient("notifications");
    if (client.BaseAddress is null)
    {
        return;
    }

    try
    {
        await client.PostAsJsonAsync("/api/notifications", new
        {
            type = "MESSAGE_RECEIVED",
            recipientUserId,
            payload = new { senderUserId, messageId = message.Id, sentAt = message.SentAt }
        }, cancellationToken);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Notification service is unavailable for message {MessageId}", message.Id);
    }
}

sealed record MongoOptions
{
    public string ConnectionString { get; init; } = "mongodb://localhost:27017";
    public string DatabaseName { get; init; } = "socialhub_messages";
    public string DialogsCollection { get; init; } = "dialogs";
}

sealed record SendMessageRequest(string Text);
sealed record ErrorResponse(string Code, string Message);
sealed record SendMessageResponse(string DialogId, MessageDocument Message);
sealed record DialogSummaryResponse(string DialogId, string[] ParticipantUserIds, DateTimeOffset LastMessageAt, string LastMessagePreview);
sealed record MessagesResponse(string DialogId, IReadOnlyCollection<MessageDocument> Messages);

sealed record DialogDocument
{
    [BsonId]
    public string Id { get; init; } = "";

    public string[] ParticipantUserIds { get; init; } = [];
    public DateTimeOffset LastMessageAt { get; init; }
    public string LastMessagePreview { get; init; } = "";
    public List<MessageDocument> Messages { get; init; } = [];
}

sealed record MessageDocument(
    string Id,
    string SenderUserId,
    string RecipientUserId,
    string Text,
    DateTimeOffset SentAt);
