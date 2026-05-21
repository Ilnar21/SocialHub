# Покрытие тест-кейсов Moderation Service

| TC | Сервис | Endpoint | Статус реализации |
|---|---|---|---|
| TC-31 | Moderation Service | `POST /api/reports` | Реализовано, покрыто unit/API тестами |
| TC-32 | Moderation Service | `POST /api/reports/{reportId}/resolve/delete-post` | Реализовано, покрыто unit-тестом обработки жалобы |
| TC-33 | Moderation Service | `POST /api/users/{userId}/blocks` | Реализовано, покрыто unit/API тестами |
| TC-34 | Moderation Service | `GET /api/private-messages/{...}` | Реализовано, покрыто API тестом запрета |
| TC-35 | Moderation Service | `POST /api/audit` | Реализовано для локальной модерации через audit endpoint |
| TC-36 | Moderation Service | `GET /api/audit` | Реализовано, аудит пишется при действиях модерации |
| TC-38 | Moderation Service | Notification side effect | Контракт подготовлен, failure сохраняется |
| TC-39 | Moderation Service | внешние Auth/Post/Notification | Основное действие не откатывается, failures сохраняются |
| TC-45 | Moderation Service | все endpoints | Добавлен `X-Correlation-Id` в response и logging scope |
