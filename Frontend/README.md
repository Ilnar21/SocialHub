# SocialHub Frontend

ASP.NET Core Razor Pages client for the integrated SocialHub stack.

## What Is Inside

- Razor Pages for login, registration, feed, communities, posts, messages, notifications, moderation and profile.
- Page scripts live in `wwwroot/js/pages`.
- Shared API, session and toast helpers live in `wwwroot/js/core`.
- Styles live in `wwwroot/css/styles.css`.

## Run

Through the shared gateway:

```bash
docker compose up --build
```

Application URL: `http://localhost:8080`

Direct frontend container URL: `http://localhost:3000`. In this mode browser API calls still go to `http://localhost:8080`.

Local run without Docker:

```bash
dotnet run --project Frontend/Frontend.csproj
```

## Pages

- `/` - login;
- `/Register` - registration;
- `/Feed` - personal feed;
- `/Communities` - communities;
- `/Posts` - create and view posts;
- `/Messages` - private messages;
- `/Notifications` - notifications;
- `/Moderation` - reports, user blocks and audit;
- `/Profile` - user profile.

## API Contract

The frontend stores the AuthService JWT in `localStorage` and sends it as:

- `Authorization: Bearer <token>`;
- `X-Correlation-Id`.

The client no longer sends `X-User-Id` or `X-User-Role`. User identity and role must come from JWT claims in backend services.
