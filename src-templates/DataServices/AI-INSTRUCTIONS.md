# AI Instructions — `src-templates/DataServices`

## Purpose

Target data-service templates. **ShowcaseDataService** is the golden, buildable scaffold source. **CustomerDataService** is a thin example only.

## Mandatory workflow

1. Read the root `README.md`, `REVIEW-REPORT.md`, and nearest parent instructions.
2. Prefer Showcase when scaffolding new domains via FindingsMigration.
3. Preserve ownership boundaries and prefer additive, reversible changes.
4. Do not invent credentials, approvals, platform capabilities, or production values.
5. Update docs, manifests, tests, RBAC, observability, and rollback guidance when behavior changes.
6. Never execute destructive SQL automatically.
7. Validate SQL-project and EF-migration ownership do not overlap.

## Current subfolders

- `ShowcaseDataService/` — golden template (FacadeThenMove, blue/green, SP wrappers)
- `CustomerDataService/` — thin example only

## Completion report

Report changed files, assumptions, validation, unresolved risks, and required approval.
