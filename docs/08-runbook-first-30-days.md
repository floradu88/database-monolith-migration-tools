# 8. First 30 Days Runbook

## Week 1

- confirm SQL Server deployment type and version;
- identify databases, replicas, failover groups, jobs, and external consumers;
- export users, roles, permissions, objects, and dependencies;
- define application identity naming standard;
- create the intelligence catalog database or central collector store.

## Week 2

- set `Application Name` for each connection string;
- introduce dedicated identities for high-priority applications;
- add session-context initialization in shared connection factories/interceptors;
- enable/configure Query Store;
- deploy scheduled DMV snapshots.

## Week 3

- deploy lightweight Extended Events in non-production;
- load-test and validate event volume/overhead;
- deploy production session with conservative filters;
- implement event-file ingestion and normalized hourly aggregation;
- start repository scanner proof of concept.

## Week 4

- correlate code references and runtime callers;
- produce first reports: unknown callers, shared identities, top procedures, never-observed objects, dynamic SQL, and cross-domain writes;
- approve the first bounded-context ownership map;
- select one pilot data service and create its façade/database project.

## Exit criteria

- at least 90% of normal application traffic attributable to an application identity;
- all SQL modules inventoried and hashed;
- Query Store and telemetry health monitored;
- repository scanner covers primary data-access libraries;
- no deletion performed yet;
- pilot scope and rollback owner approved.
