# Покрытие тест-кейсов Message Service

| TC | Сервис | Endpoint | Статус реализации |
|---|---|---|---|
| TC-26 | Message Service | `POST /api/dialogs/{recipientUserId}/messages` | Реализовано, покрыто unit/API тестами |
| TC-27 | Message Service | `GET /api/dialogs/{dialogId}/messages` | Реализовано, покрыто unit/API тестами |
| TC-28 | Message Service | `GET /api/dialogs/{dialogId}/messages` | Реализовано, покрыто unit-тестом запрета доступа |
| TC-29 | Message Service | `POST /api/dialogs/{recipientUserId}/messages` | Реализовано: Notification Service не блокирует отправку |
| TC-30 | Message Service | MongoDB persistence | Реализовано, есть условный MongoDB integration-тест |
| TC-34 | Message Service | `GET /api/dialogs/{dialogId}/messages` | Реализовано через проверку участника диалога |
| TC-43 | Message Service | MongoDB persistence | Поддержано общей MongoDB и отсутствием состояния в памяти |
| TC-45 | Message Service | все endpoints | Добавлен `X-Correlation-Id` в response и logging scope |
