# Production-Grade Gaps and Required Decisions

## Initial scope

Track:

- stored procedure execution;
- access to functions and views where observable;
- DML statement execution against database objects;
- application, SQL identity, host, session, trace ID, and deployment version;
- performance and query-plan history;
- schema, permission, and programmability-object changes.

Initially exclude row before/after values, CDC payloads, temporal history, and sensitive parameters.

## Required layers

### Telemetry by purpose

| Purpose | Mechanism |
|---|---|
| Query/plan performance | Query Store |
| Cached module counters | Persisted DMV snapshots |
| Actor/object access | SQL Audit |
| Detailed investigation | Targeted Extended Events |
| Request trace | OpenTelemetry |
| Catalog/ownership | DB Intelligence database |
| Deployment evidence | Migration ledger |

### Attribution

Every service must use:

- a dedicated SQL identity or managed identity;
- a unique `Application Name`;
- `SESSION_CONTEXT`;
- trace and span IDs;
- service name/version;
- environment;
- optional tenant/shard ID.

### Performance gates

Before and after migration, compare:

- calls per second;
- p50/p95/p99 latency;
- CPU and logical reads per call;
- writes and rows returned;
- timeout/error rate;
- blocking/deadlocks;
- plan count and variability;
- peak concurrency.

### Read-heavy architecture

Separate:

- transactional reads;
- replica-safe reads;
- cached lookups;
- search;
- reporting;
- cross-domain read models.

Avoid replacing a local join with many synchronous service calls.

### Controlled source changes

Every source-database change must be versioned, reviewed, checksummed, executed by a deployment identity, recorded in a ledger, validated, and drift-checked.
