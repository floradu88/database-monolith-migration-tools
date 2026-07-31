# RBAC and Controlled Migrations

## Platform roles

- Catalog Reader
- Application Owner
- Migration Planner
- Migration Operator
- Security Auditor
- Platform Administrator
- Break-Glass Operator

## Database roles

```text
<Service>.Runtime
<Service>.ReadOnly
<Service>.Migration
<Service>.Operations
<Service>.AuditReader
```

Runtime identities get no DDL and no `db_owner`.

## Migration evidence

Record:

```text
ChangeId
repository/commit
artifact version
script checksum
target
expected schema versions
approval
execution identity
pipeline run
outcome
validation
rollback/forward-fix
```

## Drift detection

Continuously compare live definitions, permissions, database-scoped settings, Query Store configuration, and audit configuration against source control.

Before each wave, archive exact source definitions, hashes, permissions, dependencies, and performance baselines.
