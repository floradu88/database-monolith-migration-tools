# AI Instructions — `ShowcaseDataService.Database`

## Purpose

Golden hybrid SQL database project: owns schemas, stored procedures, security stubs, and `deployment.DatabaseContract`. Does **not** own EF tables.

## Mandatory

1. Prefer additive, reversible changes; never dual-own with `ShowcaseDataService.Migrations`.
2. Keep migrated SP definitions under `Programmability/` as desired-state Build scripts.
3. Put FacadeThenMove up/down waves under `Cutover/` as `None` (not Build).
4. Document EF-owned shapes under `Reference/EfOwned/` only — do not `CREATE TABLE` them here.
5. Do not invent credentials, principals, or production connection strings.
6. Never execute destructive SQL automatically; treat scripts as DBA-review material.
7. When adding FindingsMigration stubs under `Programmability/Generated/`, add matching `<Build Include=...>` entries to the `.sqlproj`.

## Completion report

Changed files; assumptions; validation; risks; required human approvals.
