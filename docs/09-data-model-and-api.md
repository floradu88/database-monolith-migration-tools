# 9. Intelligence Catalog Data Model and API

## Core entities

```text
Application
Deployment
Repository
CodeReference
DatabaseInstance
Database
DatabaseObject
ObjectParameter
ObjectDependency
DatabaseIdentity
Permission
RuntimeObservation
QueryStoreObservation
MigrationTarget
OwnershipDecision
MigrationWave
MigrationItem
ValidationResult
DeprecationDecision
```

## Required API queries

```text
GET /applications/{id}/database-objects
GET /database-objects/{id}/callers
GET /database-objects/{id}/dependencies
GET /database-objects/{id}/runtime-history
GET /reports/unknown-callers
GET /reports/shared-write-ownership
GET /reports/deprecated-executions
GET /migration-waves/{id}/blockers
POST /ownership-decisions
POST /migration-waves
POST /migration-items/{id}/validate
POST /migration-items/{id}/cutover
POST /migration-items/{id}/rollback
```

## Runtime observation dimensions

- UTC time bucket;
- server/database/schema/object;
- application and deployment version;
- SQL identity and client host;
- execution/success/failure count;
- duration, CPU, reads, writes, row count;
- source mechanism and attribution confidence;
- event/session/query identifiers where available.

## Migration manifest

Use YAML or JSON under source control. See `src-templates/migration-manifest.example.yml`.
