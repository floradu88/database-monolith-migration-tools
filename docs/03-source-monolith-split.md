# Splitting the Existing Monolith into Manageable Projects

## Goal

Break the current SQL source into smaller projects while continuing to deploy one unchanged source database.

This reduces cognitive load and establishes ownership before physical separation.

## Recommended project types

### Foundation

Contains:

- shared SQL types;
- approved utility functions;
- foundational schemas;
- common deployment prerequisites.

### Domain projects

Contain objects clearly owned by one capability:

- tables;
- views;
- stored procedures;
- functions;
- triggers;
- permissions for that domain.

### Reference

Contains shared reference data with one accountable owner.

### Reporting

Contains read-only reporting objects and projections.

### Integration

Contains temporary integration interfaces, staging objects, and legacy exchange contracts.

### Legacy

Contains unresolved objects. Every object must have a review date.

### Composite

Builds and publishes the complete current monolith database.

## Initial split process

1. Export the current schema verbatim.
2. Normalize file layout without changing definitions.
3. Calculate hashes for every module definition.
4. Create the composite database project.
5. Move files into domain projects.
6. Add project references.
7. compile and generate a deployment script.
8. Compare generated schema with the source.
9. prove zero unintended drift.
10. make the composite project the only deployment entry point.

## Rules

- Do not rename schemas during the first split.
- Do not rewrite procedures merely to make them cleaner.
- Keep original definitions and permissions.
- One object belongs to one source project.
- Cross-project references are declared explicitly.
- Cyclic dependencies are recorded as migration blockers.
- The `Legacy` project is temporary and measured.

## Suggested file structure

```text
Monolith.Database.Customer/
├── Schemas/
├── Tables/
├── Views/
├── StoredProcedures/
├── Functions/
├── Triggers/
├── Security/
├── PreDeployment/
└── PostDeployment/
```

## Definition hash validation

For programmability objects, compare:

```text
server
database
schema
object
object type
source definition hash
project definition hash
```

No migration wave should begin until the source project accurately represents production.
