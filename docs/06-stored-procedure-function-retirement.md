# 6. Stored Procedure and Function Retirement

## Classification

Classify each module as:

- CRUD;
- set-based domain operation;
- business workflow/orchestration;
- reporting;
- batch/maintenance;
- integration;
- security/filtering;
- utility;
- unknown;
- retirement candidate.

## Removal gates

An object becomes a candidate only when:

- no current repository references exist;
- no SQL module, trigger, job, report, ETL, synonym, or linked dependency exists;
- no Query Store or Extended Events usage appears during the required period;
- no relevant DMV snapshots show usage;
- no unknown/shared caller remains;
- an accountable owner approves;
- definition, grants, dependencies, and rollback script are archived.

## Lifecycle

```text
Active
→ Candidate
→ Deprecated
→ AlertOnExecution
→ AccessRevokedForNormalApps
→ EmergencyCompatibilityOnly
→ Archived
→ Dropped
```

## Canary deprecation

Before deletion:

1. mark ownership and deprecation metadata;
2. alert when it executes;
3. remove execution permission in non-production;
4. run full automated suites and scheduled workloads;
5. revoke normal production callers while retaining an emergency role;
6. observe;
7. drop using a version-controlled migration.

Do not replace a procedure body with an error in production unless the impact and rollback are fully understood. Permission-based soft blocking is usually easier to reverse.

## Special cases

- dynamic SQL may hide names and dependencies;
- inline TVFs may be visible only through callers/plans;
- encrypted modules cannot be statically parsed without the source definition;
- external BI tools, manual scripts, and annual jobs require longer observation;
- triggers and cascading procedure calls can create indirect usage;
- synonyms and three/four-part names can obscure target ownership.
