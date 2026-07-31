# Execution Roadmap

## Phase 0 — Foundation

- confirm cloud SQL flavour;
- establish governance;
- deploy DB Intelligence database;
- define application identity standard;
- enable Query Store;
- configure initial Audit/XE;
- create source-control repositories.

## Phase 1 — Capture current state

- export source schema verbatim;
- inventory objects and permissions;
- collect procedure/function statistics;
- scan repositories with **DbIntelligence** (`src-templates/DbIntelligence/scripts/*.ps1` — see [`HOW-TO-USE.md`](../HOW-TO-USE.md));
- build application-object graph (`graph.json`, code→DB and stored-procedure maps);
- establish performance baselines.


## Phase 2 — Split the source project

- create domain SQL projects;
- create composite source project;
- move definitions without semantic changes;
- validate hashes and deployment diff;
- assign preliminary owners;
- isolate unresolved objects in Legacy.

## Phase 3 — Pilot target service

- choose a low-risk domain;
- create service projects;
- create target SQL project;
- optionally create EF migrations project;
- create runtime/migration identities;
- deploy observability and health checks.

## Phase 4 — Encapsulate and benchmark

- put façade in front of source procedures;
- add trace propagation;
- implement EF/Dapper alternatives where useful;
- run shadow reads;
- select implementation per operation.

## Phase 5 — Migrate

- deploy target schema;
- backfill;
- synchronize changes;
- reconcile;
- canary;
- cut over;
- keep rollback route.

## Phase 6 — Revoke and retire

- revoke source access;
- monitor legacy calls;
- deprecate compatibility objects;
- archive exact definitions;
- drop after observation and approval.

## Phase 7 — Scale

- tune queries;
- introduce cache/read models;
- add replicas;
- use elastic pools;
- shard only when thresholds require it.

## Pilot completion criteria

- 100% owned objects;
- known callers;
- zero critical mismatches;
- SLOs pass;
- capacity test passes;
- rollback tested;
- RBAC approved;
- audit and monitoring operational;
- source access revoked.
