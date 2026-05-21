# SocialHub Moderation Service

Микросервис модерации SocialHub.

## Назначение

- создание жалоб на публичный контент;
- обработка жалоб модератором платформы;
- блокировка пользователей;
- аудит действий модерации;
- запрет доступа к личным сообщениям;
- сохранение неудачных внешних side effects в таблицу `side_effect_failures`.

## Технологии

- .NET 8;
- ASP.NET Core Web API;
- PostgreSQL;
- Docker Compose;
- NUnit для unit/API тестов.

## Запуск

```bash
cd ModerationService
copy .env.example .env
docker compose up --build
```

Сервис: `http://localhost:5003`  
Gateway-заглушка: `http://localhost:8083/moderation`

## Переменные окружения

| Переменная | Назначение |
|---|---|
| `MODERATION_POSTGRES_CONNECTION_STRING` | строка подключения PostgreSQL |
| `POST_SERVICE_BASE_URL` | URL Post Service для удаления поста |
| `AUTH_SERVICE_BASE_URL` | URL Auth Service для блокировки пользователя |
| `NOTIFICATION_SERVICE_BASE_URL` | URL Notification Service |

## Endpoints

| Метод | Endpoint | Описание |
|---|---|---|
| `GET` | `/health` | проверка сервиса и PostgreSQL |
| `POST` | `/api/reports` | создать жалобу |
| `GET` | `/api/reports?status=NEW` | список жалоб |
| `POST` | `/api/reports/{reportId}/resolve/delete-post` | обработать жалобу удалением поста |
| `POST` | `/api/users/{userId}/blocks` | заблокировать пользователя |
| `POST` | `/api/audit` | записать действие локальной модерации |
| `GET` | `/api/audit` | получить журнал аудита |
| `GET` | `/api/private-messages/{...}` | всегда возвращает запрет доступа |

## Роли

Gateway передаёт:

- `X-User-Id` — идентификатор пользователя;
- `X-User-Role` — роль пользователя.

Платформенным модератором считается пользователь с `X-User-Role: PLATFORM_MODERATOR` или `X-User-Role: MODERATOR`.

Позже временные заголовки нужно заменить на JWT.

## Контракт с Auth Service

При блокировке пользователя сервис отправляет:

```http
POST /api/users/{blockedUserId}/status
Content-Type: application/json

{
  "status": "BLOCKED",
  "expiresAtUtc": "2026-05-28T00:00:00Z"
}
```

Ошибка Auth Service не откатывает блокировку. Неудачная попытка сохраняется как side effect со статусом `FAILED`.

## Контракт с Notification Service

Уведомление об удалении поста:

```json
{
  "type": "POST_DELETED",
  "payload": {
    "postId": "post-1",
    "reason": "Спам"
  }
}
```

Уведомление о блокировке:

```json
{
  "type": "USER_BLOCKED",
  "recipientUserId": "ivan.petrov",
  "payload": {
    "blockedUserId": "ivan.petrov",
    "reason": "Повторный спам",
    "expiresAtUtc": "2026-05-28T00:00:00Z"
  }
}
```

## Тесты

```bash
dotnet test SocialHub.Moderation.sln
```
