# Monitoring, Observability, and SLOs

## Application plane

Use OpenTelemetry for API requests, jobs, SQL client spans, queues, cache, retries, and circuit breakers.

Required attributes:

```text
service.name
service.version
deployment.environment
db.system
db.namespace
db.operation.name
db.stored_procedure.name
tenant.id
shard.id
migration.wave
legacy.or.target
```

Do not capture sensitive parameter values.

## Database plane

Monitor:

- Query Store runtime and wait stats;
- CPU, IO, workers, sessions, storage;
- connection count and pool saturation;
- blocking and deadlocks;
- plan regressions;
- failed logins and audit failures;
- throttling and replica lag.

## Migration plane

Monitor:

- source/target calls;
- shadow comparisons;
- mismatch rate;
- sync lag;
- backfill throughput;
- cutover state;
- rollback readiness;
- legacy calls after cutover.

## Golden signals

- latency;
- traffic;
- errors;
- saturation.

## Example SLOs

```text
Availability: 99.9%
Read p95: <250 ms
Write p95: <500 ms
DB timeout rate: <0.1%
Critical-field mismatch: 0
Audit delivery delay: <15 min
```
