# SocialHub Moderation Service

Moderation microservice for reports, platform blocks and audit logs.

## Responsibilities

- Register user reports for posts or other public content with status `NEW`.
- Resolve reports and record moderator actions in audit.
- Block users for a fixed duration and request status propagation to Auth Service when configured.
- Store local moderation actions from community administrators.
- Deny private-message access by policy.

## API Gateway contract

The gateway forwards:

- `X-User-Id` for authenticated identity.
- `X-User-Role` for RBAC. Platform actions require `PLATFORM_MODERATOR` or `MODERATOR`.

## Local run

```bash
docker compose up --build
```

Direct service URL: `http://localhost:5003`.
Gateway stub URL: `http://localhost:8083/moderation`.
