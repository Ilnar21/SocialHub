# SocialHub Message Service

Messaging microservice for SocialHub private dialogs.

## Responsibilities

- Send a private message and create a dialog if it does not exist.
- Return dialog history in chronological order.
- Return only dialogs where the current user is a participant.
- Keep dialog state in MongoDB so restarts and horizontal scaling do not lose history.
- Fire-and-forget notification calls when `NotificationService:BaseUrl` is configured.

## API Gateway contract

The gateway forwards authenticated identity in `X-User-Id`.

## Local run

```bash
docker compose up --build
```

Direct service URL: `http://localhost:5002`.
Gateway stub URL: `http://localhost:8082/messages`.
