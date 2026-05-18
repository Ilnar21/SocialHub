# Message Service

ASP.NET Core microservice for SocialHub private dialogs.

## Storage

MongoDB collection `dialogs` stores dialog participants, ordered messages and last message metadata. The service keeps no chat state in process memory, so history survives restarts and several service instances can share one database.

## Identity contract

The API Gateway must pass the authenticated user id in `X-User-Id`.

## Endpoints

- `GET /health`
- `POST /api/dialogs/{recipientUserId}/messages`
- `GET /api/dialogs`
- `GET /api/dialogs/{dialogId}/messages?skip=0&limit=50`

## Configuration

```json
{
  "Mongo": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "socialhub_messages",
    "DialogsCollection": "dialogs"
  },
  "NotificationService": {
    "BaseUrl": ""
  }
}
```
