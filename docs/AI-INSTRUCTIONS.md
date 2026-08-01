# AI Instructions — `docs`

## Purpose

This folder is part of the reviewed SQL Server monolith-decomposition and DB-as-a-Service kit. Its role is determined by its path and local README/documentation.

## Mandatory workflow

1. Read the root `README.md`, `HOW-TO-USE.md`, `REVIEW-REPORT.md`, and nearest parent instructions.
2. Preserve ownership boundaries and prefer additive, reversible changes.
3. Do not invent credentials, approvals, platform capabilities, or production values.
4. Update docs, manifests, tests, RBAC, observability, and rollback guidance when behavior changes.
5. Never execute destructive SQL automatically.
6. Validate SQL-project and EF-migration ownership do not overlap.
7. Keep local DbIntelligence ops docs aligned with `HOW-TO-USE.md` (fnm Node, Codegraph via `fnm exec`, batch parents `D:\code\projects` / `C:\code`).

## Current subfolders

- None.

## Current files

- `00-master-plan.md`
- `01-architecture.md`
- `01-target-architecture.md`
- `02-project-structure.md`
- `02-solution-and-project-structure.md`
- `03-discovery-and-ai-indexing.md`
- `dbintelligence-runbook.md`
- `FUTURE-FEATURES.md`
- `03-source-monolith-split.md`
- `04-target-database-project-strategy.md`
- `04-usage-tracking-strategy.md`
- `05-migration-control-plane.md`
- `05-migration-plan.md`
- `06-stored-procedure-function-retirement.md`
- `06-usage-tracking-and-audit.md`
- `07-data-access-strategy.md`
- `07-security-observability-nfr.md`
- `08-performance-monitoring-and-scaling.md`
- `08-runbook-first-30-days.md`
- `09-data-model-and-api.md`
- `09-rbac-security-and-change-control.md`
- `10-decisions-and-risks.md`
- `10-execution-roadmap.md`
- `10-production-grade-gaps-and-decisions.md`
- `11-data-access-ab-evaluation.md`
- `11-tooling-and-version-policy.md`
- `12-monitoring-observability-and-slos.md`
- `12-platform-decision-record.md`
- `13-scaling-read-heavy-services-and-sharding.md`
- `14-rbac-and-controlled-migrations.md`
- `15-dml-and-programmability-access-tracking.md`
- `16-production-decomposition-runbook.md`
- `CANONICAL-DOCUMENT-INDEX.md`

## Completion report

Report changed files, assumptions, validation, unresolved risks, and required approval.
