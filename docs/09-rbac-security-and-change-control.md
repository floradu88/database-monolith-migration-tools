# RBAC, Security, and Change Control

## Platform RBAC

- Catalog Reader
- Application Owner
- Migration Planner
- Migration Operator
- Security Auditor
- Platform Administrator
- Break-Glass Operator

## Database RBAC

Per service:

- Runtime
- ReadOnly
- Migration
- Operations
- AuditReader

## Rules

- runtime has no DDL;
- runtime is not `db_owner`;
- migration identity is separate and short-lived;
- support access is just-in-time;
- break-glass access expires and is audited;
- no credentials in repositories;
- managed identity preferred;
- secrets rotated;
- schema access follows ownership.

## Change control

Every deployment records:

- change ID;
- commit;
- artifact version;
- SHA-256;
- database;
- wave;
- approver;
- executor;
- pipeline;
- validation result;
- rollback or forward-fix.

## Drift

Detect:

- object definition drift;
- permission drift;
- configuration drift;
- Query Store drift;
- audit drift;
- schema version drift.

Unmanaged drift blocks automated cutover.
