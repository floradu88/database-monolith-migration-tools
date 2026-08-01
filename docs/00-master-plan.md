# Master Plan

## Objective

Split a large cloud-hosted SQL Server database monolith into smaller production-grade databases owned by data-access microservices.

The solution must provide:

- complete inventory and runtime usage evidence;
- manageable source projects for the existing monolith;
- independently deployable target database projects;
- optional EF Core migrations for service-owned schema changes;
- safe data migration and cutover;
- actor and application traceability;
- performance baselines and regression control;
- RBAC and controlled production changes;
- horizontal service scaling;
- read scaling and eventual sharding.

## Workstreams

### Workstream A — Discovery and DB Intelligence

Build the catalog of:

- applications and repositories;
- SQL connections and identities;
- stored procedures and functions;
- views, triggers, synonyms, and jobs;
- DML and EXECUTE usage;
- SQL dependencies;
- performance baselines;
- ownership and target-domain suggestions.

**Local implementation:** `src-templates/DbIntelligence/` (API + Angular + scanners + PowerShell scripts). See root [`HOW-TO-USE.md`](../HOW-TO-USE.md). Prefer user-scoped **fnm** for Node/npm (`scripts/Initialize-DbIntelligenceNode.ps1`); install **Codegraph** with `fnm exec -- npm i -g @colbymchenry/codegraph` when fnm is present.


### Workstream B — Split the existing database project

Create a source-of-truth database solution that represents the current monolith verbatim, but split into manageable domain projects.

The first split is organizational, not physical.

Example:

```text
Monolith.Database.Foundation
Monolith.Database.Customer
Monolith.Database.Billing
Monolith.Database.Ordering
Monolith.Database.Reference
Monolith.Database.Reporting
Monolith.Database.Integration
Monolith.Database.Legacy
Monolith.Database.Deployment
```

All projects still publish to the same source database initially.

### Workstream C — DB-as-a-Service target

For each bounded context, create:

- service API;
- application layer;
- domain layer where needed;
- infrastructure/DAL;
- contracts;
- SQL database project;
- optional EF Core migrations project;
- integration, contract, and database tests.

### Workstream D — Migration control plane

Create a dedicated project that:

- stores source-to-target mappings;
- orchestrates schema deployment;
- performs backfill;
- validates and reconciles;
- manages shadow traffic and cutovers;
- records approvals and evidence;
- detects drift;
- supports rollback.

### Workstream E — Production operations

Implement:

- Query Store;
- SQL Audit;
- Extended Events;
- OpenTelemetry;
- dashboards and alerts;
- SLOs;
- capacity planning;
- read replicas and elastic pools;
- shard-map and resharding strategy.

## Core decision

Use a hybrid database-change model:

- SQL database projects for deterministic, reviewable database object ownership;
- EF Core migrations where the service primarily owns relational aggregate persistence;
- Dapper or stored procedures for explicit and performance-sensitive access;
- do not allow each tool to independently own the same object.

## Definition of success

A domain is successfully extracted when:

- every database object has a confirmed owner;
- all callers are known;
- all runtime access uses dedicated identities;
- target performance meets SLOs;
- reconciliation passes;
- rollback is rehearsed;
- source access is revoked;
- monitoring and on-call ownership are active;
- the source compatibility layer can be retired.
