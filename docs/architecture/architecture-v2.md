# Architecture Version 2

This document is the technical map for the English Center Platform roadmap.
The companion [SVG diagram](architecture-v2.svg) uses three visual states:

- **Implemented** — M1–M5 code and local infrastructure already verified.
- **Planned** — M6–M9 components approved for later milestones.
- **Future / advanced** — M10 options that are not initial core dependencies.

## System boundaries

The browser uses the React and TypeScript single-page application. Nginx serves
the production frontend and proxies relative `/api` requests to the ASP.NET
Core Web API. The API is a modular monolith: business modules share one
deployment while their controllers, contracts, services, and registrations stay
separated by module.

SQL Server is the operational database and the **OLTP source of truth** for
students, teachers, courses, classes, enrollments, attendance, payments,
authentication, refresh tokens, and notification processing records.

Redis contains short-lived cache and rate-limit state. It is not a durable
business-data store. RabbitMQ transports durable notification integration
events. The worker uses manual acknowledgement, a SQL Server idempotency ledger,
delayed retry, and a dead-letter queue.

## Implemented M1–M5 request flow

1. The user opens the React application through Nginx.
2. React sends a relative `/api` request with a JWT access token when required.
3. Nginx proxies the request to the ASP.NET Core API.
4. Authorization policies enforce ADMIN, STAFF, TEACHER, and STUDENT access.
5. Business services read and write SQL Server through EF Core.
6. Course queries use Redis cache-aside behavior. Login, refresh, and
   notification endpoints use the Redis fixed-window rate limiter.
7. Notification requests are confirmed by RabbitMQ before the API returns
   `202 Accepted`.
8. The notification worker records each EventId in SQL Server before
   acknowledging the message. Failed work retries three times before the DLQ.

## Planned M6 data pipeline

The batch pipeline is directional:

```text
SQL Server OLTP
  -> Python incremental extraction using an updated_at watermark
  -> MinIO RAW/Bronze immutable landing data
  -> Spark/PySpark validation and transformation
  -> PostgreSQL analytical DWH
```

Airflow orchestrates extraction, landing, transformation, load, data-quality
checks, and reconciliation. PostgreSQL stores analytical models derived from
SQL Server data. It is **not** a one-to-one replica and does not replace SQL
Server for application transactions.

## Planned M7 analytics

PostgreSQL will contain dimensions, facts, star schemas, and data marts with an
explicit grain. Data-quality and reconciliation checks compare source totals,
row counts, keys, and financial measures before Power BI datasets are accepted.

## Planned M8 observability

OpenTelemetry will emit traces and metrics from services. Prometheus stores
metrics, Loki stores logs, and Grafana presents dashboards and investigation
views. Alert rules are introduced only after useful service-level indicators
are defined.

## Planned M9 AI assistant

The AI assistant may use Ollama for local model inference, pgvector for
embeddings, retrieval-augmented generation (RAG), and an AI API implemented in
.NET or Python. Langfuse records AI traces and evaluations. Text-to-SQL must
query an approved analytical semantic layer with read-only credentials, query
limits, and validation; it must not write to SQL Server OLTP.

## M10 future / advanced options

Debezium change-data capture and Kafka streaming are future/advanced options.
They are considered only after the incremental batch pipeline is stable and a
measured latency requirement justifies streaming complexity. dbt is optional
when it adds clear value for SQL models, tests, or lineage without duplicating
PySpark transformations.

Kubernetes, Terraform, and Cloud/Lakehouse targets are also future/advanced
deployment options. The current supported development environment is local
Docker Compose.

## Security and secret handling

- Commit only `.env.example` files containing placeholders.
- Keep real passwords, signing keys, and tokens in ignored local environment
  files or an external secret manager.
- Never copy access tokens, refresh tokens, connection-string passwords, or
  private keys into documentation, issues, logs, screenshots, or spreadsheets.
- Use least-privilege identities for future ETL, BI, observability, and AI
  components.

## Decision summary

| Decision | Reason |
| --- | --- |
| ASP.NET Core modular monolith | Keeps the first application deployable while preserving module boundaries. |
| React + TypeScript | Provides typed browser contracts and reusable management screens. |
| SQL Server as OLTP source of truth | Owns transactional application state and concurrency. |
| Redis for cache and rate limits | Speeds reads and limits selected endpoints without becoming a business-data authority. |
| RabbitMQ for notification events | Provides durable asynchronous delivery, retry, and DLQ behavior. |
| MinIO RAW/Bronze before transformation | Preserves replayable source extracts and separates landing from curated data. |
| PostgreSQL as analytical DWH | Stores dimensional models and data marts rather than an OLTP copy. |
| Debezium/Kafka deferred to M10 | Avoids streaming complexity before batch requirements and correctness are proven. |
