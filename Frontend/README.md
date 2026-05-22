# SocialHub Frontend

ASP.NET Core Razor Pages клиент для интеграционной ветки SocialHub.

## Что внутри

- Razor Pages для входа, регистрации, ленты, сообществ, постов, сообщений, уведомлений, модерации и профиля.
- Отдельные page-скрипты в `wwwroot/js/pages`, общие функции API/сессии/уведомлений в `wwwroot/js/core`.
- Стили в `wwwroot/css/styles.css`.
- Dev fallback заголовки `X-User-Id` и `X-User-Role` пока оставлены для сервисов, где они еще используются.

## Запуск

Через общий gateway:

```bash
docker compose up --build
```

Открыть приложение: `http://localhost:8080`

Прямой адрес frontend-контейнера: `http://localhost:3000`. В этом режиме браузер отправляет API-запросы на `http://localhost:8080`.

Локально без Docker:

```bash
dotnet run --project Frontend/Frontend.csproj
```

## Страницы

- `/` - вход;
- `/Register` - регистрация;
- `/Feed` - персональная лента;
- `/Communities` - сообщества;
- `/Posts` - создание и просмотр постов;
- `/Messages` - личные сообщения;
- `/Notifications` - уведомления;
- `/Moderation` - жалобы, блокировки, аудит;
- `/Profile` - профиль пользователя.

## API-контракты

Клиент работает через gateway и хранит JWT в `localStorage`. В запросы добавляются:

- `Authorization: Bearer <token>`;
- `X-User-Id`;
- `X-User-Role`;
- `X-Correlation-Id`.

`X-User-Id` и `X-User-Role` нужны только для текущего dev fallback. Позже их можно убрать после полного перехода сервисов на JWT.
