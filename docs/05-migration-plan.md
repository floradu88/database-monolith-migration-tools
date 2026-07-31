# 5. Migration Plan

## Phase 0 — Governance and baseline

- name accountable application/database owners;
- identify cloud SQL flavour and platform limitations;
- create object registry and decision workflow;
- define RPO/RTO, security classification, approval gates, and rollback standards;
- prohibit new shared credentials and unapproved direct database access.

## Phase 1 — Discover

- inventory repositories and deployed applications;
- inventory SQL objects, jobs, reports, ETL, identities, permissions, synonyms, and linked dependencies;
- enable attribution, Query Store, DMV snapshots, and lightweight Extended Events;
- build the static/runtime dependency graph.

## Phase 2 — Classify and assign ownership

- classify objects by business capability and access type;
- identify tables with multiple write owners;
- identify cross-domain procedures and transactions;
- assign confirmed owner and target namespace;
- place unresolved objects in a temporary `legacy` classification.

## Phase 3 — Logical isolation

- create domain schemas in the existing database;
- create dedicated application identities;
- move or recreate owned procedures/functions under target schemas;
- introduce compatibility views/procedures only with an expiry date;
- revoke broad permissions progressively.

## Phase 4 — Encapsulate access

- introduce a data-service façade;
- redirect consumers from direct SQL to API/gRPC/command/event contracts;
- keep the façade calling legacy SQL initially if necessary;
- add contract tests and end-to-end correlation.

## Phase 5 — Extract storage

- provision the target database;
- deploy target schema and security;
- backfill authoritative data;
- synchronize changes using an outbox, CDC, or controlled pipeline;
- execute shadow reads and reconciliation;
- cut over writes, then reads, using explicit gates.

## Phase 6 — Revoke and clean up

- revoke legacy identities and cross-schema rights;
- monitor attempts to execute deprecated objects;
- archive definitions, grants, plans, and rollback scripts;
- remove compatibility objects and source objects through version-controlled migrations.

## Migration state machine

```text
Discovered
→ Observed
→ OwnershipProposed
→ OwnershipApproved
→ TargetDesigned
→ TargetDeployed
→ Backfilled
→ Reconciled
→ ShadowTraffic
→ ConsumerCutover
→ LegacyAccessRevoked
→ Deprecated
→ Removed
```

Each transition stores evidence, approver, timestamp, validation results, and rollback location.

## Pilot selection

Choose a bounded capability with clear ownership, manageable volume, few shared writes, good test coverage, and moderate business criticality. Avoid central identity, core ledger, or the most entangled domain as the first extraction.
