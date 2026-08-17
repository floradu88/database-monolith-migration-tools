# AI Instructions — `sql/common`

## Purpose

This folder is part of the reviewed SQL Server monolith-decomposition and DB-as-a-Service kit. Its role is determined by its path and local README/documentation.

## Mandatory workflow

1. Read the root `README.md`, `REVIEW-REPORT.md`, and nearest parent instructions.
2. Preserve ownership boundaries and prefer additive, reversible changes.
3. Do not invent credentials, approvals, platform capabilities, or production values.
4. Update docs, manifests, tests, RBAC, observability, and rollback guidance when behavior changes.
5. Never execute destructive SQL automatically.
6. Validate SQL-project and EF-migration ownership do not overlap.

## Current subfolders

- None.

## Current files

- `00-preflight.sql`
- `01-create-telemetry-schema.sql`
- `02-enable-query-store.sql`
- `03-snapshot-dmv-usage.sql`
- `04-inventory-objects-and-dependencies.sql`
- `05-current-cache-reports.sql`
- `06-session-attribution.sql`
- `07-query-store-procedure-analysis.sql`
- `20-create-deployment-ledger.sql`
- `21-create-rbac-roles.sql`
- `22-query-store-performance-baseline.sql`
- `23-object-and-dml-audit-spec-template.sql`
- `24-database-capacity-snapshot.sql`
- `25-usage-aggregation-contract.sql`
- `26-object-definition-snapshot.sql`
- `27-permission-snapshot.sql`
- `28-schema-drift-hash.sql`
- `29-session-context-bootstrap.sql`
- `30-post-migration-legacy-access-report.sql`
- `31-ddl-security-audit-template.sql`
- `40-create-core-schema.sql`
- `41-clone-table-to-core.sql`
- `42-register-dual-write-pair.sql`
- `43-core-sp-clone-template.sql`
- `44-table-integrity-check.sql`
- `45-dual-write-rbac.sql`

## Completion report

Report changed files, assumptions, validation, unresolved risks, and required approval.
