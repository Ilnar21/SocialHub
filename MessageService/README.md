# SocialHub Message Service

Микросервис личных сообщений SocialHub.

## Назначение

- отправка личного сообщения;
- создание диалога при первом сообщении;
- получение списка диалогов пользователя;
- получение истории сообщений;
- запрет доступа к чужому диалогу;
- хранение истории в MongoDB;
- асинхронное уведомление Notification Service без ожидания ответа.

## Технологии

- .NET 8;
- ASP.NET Core Web API;
- MongoDB;
- Docker Compose;
- NUnit для unit/API тестов.

## Запуск

```bash
cd MessageService
copy .env.example .env
docker compose up --build
```

Сервис: `http://localhost:5002`  
Gateway-заглушка: `http://localhost:8082/messages`

## Переменные окружения

| Переменная | Назначение |
|---|---|
| `MESSAGE_MONGO_CONNECTION_STRING` | строка подключения MongoDB |
| `MESSAGE_MONGO_DATABASE` | имя БД сообщений |
| `MESSAGE_MONGO_DIALOGS_COLLECTION` | коллекция диалогов |
| `NOTIFICATION_SERVICE_BASE_URL` | URL Notification Service, может быть пустым |

## Endpoints

| Метод | Endpoint | Описание |
|---|---|---|
| `GET` | `/health` | проверка сервиса и MongoDB |
| `POST` | `/api/dialogs/{recipientUserId}/messages` | отправить сообщение |
| `GET` | `/api/dialogs` | список диалогов текущего пользователя |
| `GET` | `/api/dialogs/{dialogId}/messages?skip=0&limit=50` | история сообщений |

Gateway должен передавать `X-User-Id`. Позже этот временный контракт нужно заменить на JWT.

## Формат dialogId

`dialogId` строится детерминированно из двух идентификаторов участников:

```text
dialog_{user_a}_{user_b}
```

Участники сортируются без учёта регистра, не буквенно-цифровые символы заменяются на `_`.

Пример:

```text
ivan.petrov + maria.sokolova -> dialog_ivan_petrov_maria_sokolova
```

Историю диалога может читать только пользователь, чей `X-User-Id` входит в список участников диалога.

## Ограничения

- пустые сообщения запрещены;
- максимальная длина сообщения: `4000` символов;
- Notification Service вызывается асинхронно и не блокирует отправку сообщения.

## Тесты

```bash
dotnet test SocialHub.Message.sln
```

MongoDB integration-тест запускается, если доступен `MESSAGE_TEST_MONGO` или локальный `mongodb://localhost:27017`; иначе тест корректно пропускается.
