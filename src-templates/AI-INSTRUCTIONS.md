# AI Instructions — `src-templates`

## Purpose

This folder is part of the reviewed SQL Server monolith-decomposition and DB-as-a-Service kit. Its role is determined by its path and local README/documentation.

## Mandatory workflow

1. Read the root `README.md`, `HOW-TO-USE.md`, `REVIEW-REPORT.md`, and nearest parent instructions.
2. Preserve ownership boundaries and prefer additive, reversible changes.
3. Do not invent credentials, approvals, platform capabilities, or production values.
4. Update docs, manifests, tests, RBAC, observability, and rollback guidance when behavior changes.
5. Never execute destructive SQL automatically.
6. Validate SQL-project and EF-migration ownership do not overlap.
7. For DbIntelligence, prefer `DbIntelligence/scripts/*.ps1` and user-scoped Node (`Initialize-DbIntelligenceNode.ps1` / fnm) over inventing install steps.

## Current subfolders

- `BuildingBlocks/`
- `DataServices/`
- `DbIntelligence/`
- `FindingsMigration/`
- `MigrationControlPlane/`
- `SourceMonolith/`
- `Tests/`

## Current files

- `DatabaseModernization.sln`
- `Directory.Build.props`
- `migration-manifest.example.yml`
- `solution-tree.txt`

## Completion report

Report changed files, assumptions, validation, unresolved risks, and required approval.
