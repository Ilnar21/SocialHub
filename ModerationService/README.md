# SocialHub Moderation Service

Moderation Service handles reports, platform-moderator actions, user blocks, audit records and failed external side effects.

## Auth

User-facing endpoints require:

```http
Authorization: Bearer <jwt>
```

The current user id and role are read from JWT claims. The service no longer trusts `X-User-Id` or `X-User-Role`.

Platform moderator actions require the `PlatformModerator` role claim.

## Endpoints

- `GET /health`
- `POST /api/reports`
- `GET /api/reports?status=NEW` - platform moderator only
- `POST /api/reports/{reportId}/resolve/delete-post` - platform moderator only
- `POST /api/users/{userId}/blocks` - platform moderator only
- `GET /api/audit` - platform moderator only
- `POST /api/audit` - platform moderator only
- `GET /api/private-messages/{...}` - authenticated deny endpoint

## External Side Effects

The service calls Auth, Post and Notification services with `X-Internal-Token`.

## Tests

```bash
dotnet test SocialHub.Moderation.sln
```
