# AI Instructions — `src-templates/DbIntelligence`

## Purpose

Concrete DbIntelligence implementation: orchestrate Codegraph/Graphify CLIs, scan code→SQL/SP usage, merge evidence graphs, serve APIs, and render Angular graph UI.

## Mandatory workflow

1. Read the root `README.md`, `HOW-TO-USE.md`, `REVIEW-REPORT.md`, and this folder's `README.md`.
2. Preserve ownership boundaries and prefer additive, reversible changes.
3. Do not invent credentials, approvals, platform capabilities, or production values.
4. Update docs, manifests, tests, RBAC, observability, and rollback guidance when behavior changes.
5. Never execute destructive SQL automatically; SqlScanner stays read-only.
6. Validate SQL-project and EF-migration ownership do not overlap.
7. Keep Graphify-compatible export shapes (`graph.json`, confidence labels) stable for the Angular UI.
8. Prefer `scripts/*.ps1` for local setup/run documentation (PowerShell-first).
9. Treat the live store as **in-memory only** (`FileIntelligenceStore`); do not assume a database catalog exists. Durable output is JSON/MD export under artifacts.


## Current subfolders

- `DbIntelligence.Api/`
- `DbIntelligence.Cli/`
- `DbIntelligence.Contracts/`
- `DbIntelligence.Domain/`
- `DbIntelligence.Infrastructure/`
- `DbIntelligence.RepositoryScanner/`
- `DbIntelligence.SqlScanner/`
- `DbIntelligence.Worker/`
- `DbIntelligence.Web/`
- `DbIntelligence.Tests/`
- `scripts/`

## Current files

- `README.md`

## Completion report

Report changed files, assumptions, validation, unresolved risks, and required approval.
