# Usage Tracking and Audit

## What must be tracked

- procedure executions;
- observable function usage;
- view access;
- trigger execution;
- INSERT, UPDATE, DELETE, MERGE;
- selected SELECT access;
- dynamic SQL entry points;
- database user and application identity;
- host, session, trace ID, version, tenant/shard;
- success/failure;
- duration, CPU, reads, writes;
- schema, permission, and object changes.

## Mechanism matrix

| Requirement | Mechanism |
|---|---|
| Long-term query/plan performance | Query Store |
| Procedure/function cache snapshots | DMVs persisted externally |
| Who accessed what | SQL Audit |
| Short deep investigation | Extended Events |
| Application request correlation | OpenTelemetry + SESSION_CONTEXT |
| Static dependencies | SQL parser + metadata |
| Deployment actions | Change ledger |
| Live/source drift | schema comparison |

## Attribution priority

1. dedicated managed identity/login;
2. trusted session context;
3. connection-string application name;
4. host/deployment mapping;
5. query signature inference.

## High read-volume policy

Do not begin by auditing every SELECT across the database.

Use:

- schema-scoped EXECUTE;
- schema-scoped INSERT/UPDATE/DELETE;
- SELECT only for critical schemas or discovery windows;
- Query Store for broad read performance;
- sampled or targeted XE;
- hourly aggregation;
- raw audit retention outside the transactional database.

## Function caveat

Inline TVFs and inlined scalar UDFs can be merged into caller plans. Their usage must be inferred from:

- source references;
- module dependencies;
- Query Store text/plans;
- caller usage;
- targeted XE.

Absence from a function DMV is not proof of non-use.
