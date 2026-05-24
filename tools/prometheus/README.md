# Observability

Prometheus is included in the root Docker Compose stack and scrapes every public application container.

## Endpoints

- Prometheus UI: `http://localhost:9090`
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

For TC-45 verification, run a user flow through `http://localhost:8080`, then search container logs by the returned `X-Correlation-Id`.
