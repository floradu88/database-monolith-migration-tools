# Migration Control Plane

## Responsibilities

- import DB Intelligence mappings;
- define target ownership;
- create migration waves;
- deploy target schemas;
- run backfills;
- coordinate synchronization;
- perform reconciliation;
- control feature flags;
- initiate shadow reads and canaries;
- execute cutover;
- monitor legacy calls;
- manage rollback;
- record evidence.

## Domain model

```text
MigrationProgram
MigrationWave
MigrationItem
SourceObject
TargetObject
Dependency
BackfillJob
SynchronizationJob
ValidationRule
CutoverPlan
RollbackPlan
Approval
DeploymentArtifact
Evidence
```

## State machine

```text
Discovered
→ SourceCaptured
→ UsageObserved
→ OwnershipApproved
→ TargetDesigned
→ TargetDeployed
→ BackfillRunning
→ Synchronizing
→ ReconciliationPassed
→ ShadowTraffic
→ Canary
→ CutoverApproved
→ CutoverComplete
→ LegacyAccessRevoked
→ SourceDeprecated
→ SourceRemoved
```

## Migration strategies

- façade over source;
- schema-first extraction;
- snapshot plus delta;
- application outbox;
- CDC where later approved;
- dual-read comparison;
- short-lived controlled dual write;
- **parallel dbo + core stored procedures** (same database; dbo is caller result; core is SP-write-only subset; extra dbo rows from other writers are expected — see `sql/common/40`–`45` and `checklists/dbo-to-core-sp-quality.md`);
- event-driven projection;
- read-only extraction.

## Controlled source changes during migration

During an active migration wave:

- source object changes require migration-team review;
- the source definition is re-hashed;
- compatibility impact is recalculated;
- target mapping is revalidated;
- new consumers are blocked or explicitly registered;
- drift prevents cutover.

## Rollback levels

1. route traffic back;
2. disable target writes;
3. restore source authority;
4. reconcile operations performed during canary;
5. retain target for investigation;
6. record failed-wave evidence.
