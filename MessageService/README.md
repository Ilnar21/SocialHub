# SocialHub Message Service

Message Service stores private dialogs in MongoDB and exposes authenticated dialog APIs.

## Auth

All user-facing endpoints require:

```http
Authorization: Bearer <jwt>
```

The service reads the current user id from JWT claims (`nameidentifier` / `sub`). It no longer trusts `X-User-Id`.

## Endpoints

- `GET /health`
- `POST /api/dialogs/{recipientUserId}/messages`
- `GET /api/dialogs`
- `GET /api/dialogs/{dialogId}/messages?skip=0&limit=50`

Only dialog participants can read message history.

## Tests

```bash
dotnet test SocialHub.Message.sln
```
