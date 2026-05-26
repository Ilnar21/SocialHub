# Observability

Prometheus is included in the root Docker Compose stack and scrapes every public application container.

## Endpoints

- Prometheus UI: `http://localhost:9090`
- Grafana UI: `http://localhost:3001`
- Loki API: `http://localhost:3100`
- Gateway exporter metrics: `http://localhost:9113/metrics`
- Service metrics:
  - `http://localhost:5000/metrics` - Auth & User Service
  - `http://localhost:5001/metrics` - Community Service
  - `http://localhost:5002/metrics` - Message Service
  - `http://localhost:5003/metrics` - Moderation Service
  - `http://localhost:5004/metrics` - Notification Service
  - `http://localhost:5005/metrics` - Feed Service
  - `http://localhost:5006/metrics` - Post Service
  - `http://localhost:3000/metrics` - Frontend

## Logging

Gateway creates or forwards `X-Correlation-Id`, includes it in the response, and writes it to access logs.
Application services add the same correlation id to response headers, request log scope, completion logs, and outgoing internal HTTP calls.
Application and gateway logs are written as JSON and collected by Promtail into Loki.

For TC-45 verification, run a user flow through `http://localhost:8080`, copy the returned `X-Correlation-Id`, then open Grafana dashboard `SocialHub / SocialHub Logs` and paste the id into the `Search` field.

## Grafana

Grafana starts with a preconfigured Prometheus datasource named `Prometheus`.
Local default login is `admin` / `local_grafana_password`, and anonymous viewer access is enabled for quick project demos.
Provisioned dashboard: `SocialHub / SocialHub Overview`.
Provisioned logs dashboard: `SocialHub / SocialHub Logs`.

The overview dashboard shows service availability, gateway traffic, application request rate, p95 latency, active requests, process CPU, process memory, .NET memory and 5xx errors.

## Alerts

Prometheus loads rules from `tools/prometheus/alert-rules.yml`.
The current rules cover unavailable services, high 5xx rate, high p95 latency and gateway traffic silence.
They are visible in Prometheus at `http://localhost:9090/alerts`.
