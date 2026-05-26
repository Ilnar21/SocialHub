# Наблюдаемость

Prometheus включен в корневой Docker Compose стек и собирает метрики со всех публичных контейнеров приложения.

## Эндпоинты

- Prometheus UI: `http://localhost:9090`
- Grafana UI: `http://localhost:3001`
- Loki API: `http://localhost:3100`
- Tempo API: `http://localhost:3200`
- Tempo OTLP gRPC: `http://localhost:4317`
- Tempo OTLP HTTP: `http://localhost:4318`
- Метрики gateway exporter: `http://localhost:9113/metrics`
- Метрики сервисов:
  - `http://localhost:5000/metrics` - Auth & User Service
  - `http://localhost:5001/metrics` - Community Service
  - `http://localhost:5002/metrics` - Message Service
  - `http://localhost:5003/metrics` - Moderation Service
  - `http://localhost:5004/metrics` - Notification Service
  - `http://localhost:5005/metrics` - Feed Service
  - `http://localhost:5006/metrics` - Post Service
  - `http://localhost:3000/metrics` - Frontend

## Логирование

Gateway создает или пробрасывает `X-Correlation-Id`, добавляет его в ответ и пишет в access logs.
Сервисы приложения добавляют тот же correlation id в response headers, scope логов, логи завершения запросов и исходящие внутренние HTTP-вызовы.
Логи приложения и gateway пишутся в JSON-формате и собираются Promtail в Loki.
Когда активен OpenTelemetry activity, в логах приложения также появляются `TraceId` и `SpanId`.
Gateway пишет путь запроса без query-параметров, а trace tags приложения удаляют query string и чувствительные headers перед экспортом.

Для проверки TC-45 нужно выполнить пользовательский сценарий через `http://localhost:8080`, скопировать возвращенный `X-Correlation-Id`, открыть в Grafana dashboard `SocialHub / SocialHub Logs` и вставить id в поле `Search`.

## Трейсинг

Tempo хранит OpenTelemetry traces, которые экспортируют все ASP.NET Core сервисы и frontend.
Экспорт трейсов включается в Docker Compose через `OTEL_EXPORTER_OTLP_ENDPOINT=http://tempo:4317`.
Grafana запускается с заранее настроенным datasource `Tempo`.

Чтобы проверить distributed tracing, выполните пользовательский сценарий через `http://localhost:8080`, затем откройте Grafana `Explore`, выберите datasource `Tempo`, режим `Search`, сервис например `auth-service`, `community-service`, `post-service` или `feed-service`, и запустите поиск.
В открытом trace будут видны входящие HTTP spans и внутренние `HttpClient` вызовы между сервисами.
Из trace span Grafana может перейти к Loki logs за тот же временной промежуток, используя trace id и service label.

## Grafana

Grafana запускается с заранее настроенным datasource `Prometheus`.
Datasource для трейсов: `Tempo`.
Datasource для логов: `Loki`.
Локальный логин по умолчанию: `admin` / `local_grafana_password`.
Для быстрой демонстрации проекта включен анонимный доступ с ролью Viewer.
Подготовленный dashboard: `SocialHub / SocialHub Overview`.
Подготовленный dashboard логов: `SocialHub / SocialHub Logs`.

Overview dashboard показывает доступность сервисов, трафик gateway, частоту запросов приложения, p95 latency, активные запросы, CPU процесса, память процесса, .NET memory и 5xx ошибки.

## Alerts

Prometheus загружает правила из `tools/prometheus/alert-rules.yml`.
Текущие правила покрывают недоступность сервисов, высокий процент 5xx, высокий p95 latency и отсутствие трафика через gateway.
Они доступны в Prometheus по адресу `http://localhost:9090/alerts`.
