# 7. Security, Observability, and Non-Functional Requirements

## Security

- dedicated managed identity/login per runtime application;
- separate migration, read-only, and support identities;
- least privilege at schema/object level;
- no application `db_owner`;
- secrets in a managed vault and regularly rotated;
- TLS enforced and public access restricted;
- audited privilege and schema changes;
- redact sensitive parameter values from tracing;
- classify PII, financial, authentication, and regulated columns;
- do not embed production values in the AI index.

## Availability and resilience

- define RPO/RTO per target data service;
- automated backups and restore drills;
- connection resiliency with bounded retries and jitter;
- idempotent commands and event handlers;
- outbox/inbox for reliable messaging;
- explicit timeout and circuit-breaker policies;
- cutover and rollback runbooks tested before production.

## Performance

Capture baseline and target:

- p50/p95/p99 latency;
- CPU, logical reads/writes, row counts;
- lock waits, blocking, deadlocks;
- connection-pool saturation;
- Query Store regressions;
- event/CDC lag;
- API overhead compared with direct SQL.

Avoid replacing one SQL join with hundreds of chatty API requests. Use coarse-grained endpoints, batch operations, local read models, and events.

## Observability

Every request should correlate:

```text
API trace → command/query handler → SQL connection/session
→ stored procedure/query → event/outbox → downstream consumer
```

Minimum dashboards:

- database object usage by application;
- unknown callers;
- deprecated-object executions;
- top CPU/read/write procedures;
- migration reconciliation failures;
- cutover error and latency deltas;
- cross-domain writes and unauthorized access attempts.
