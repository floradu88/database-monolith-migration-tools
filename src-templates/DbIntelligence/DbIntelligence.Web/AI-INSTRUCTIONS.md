# AI Instructions — `src-templates/DbIntelligence/DbIntelligence.Web`

## Purpose

Angular SPA for Graphify-like interactive evidence graphs (vis-network), code→DB maps, and index job controls.

## Mandatory workflow

1. Read parent `DbIntelligence/AI-INSTRUCTIONS.md` and `DbIntelligence/README.md`.
2. Keep API contracts aligned with `DbIntelligence.Contracts` / Api endpoints.
3. Do not hard-code secrets or production repository paths in the UI.
4. Prefer additive UI changes that preserve search + canvas + detail composition.
5. Prefer indexing via `../scripts/Invoke-DbIntelligenceReady.ps1 "<project-path>"` (starts API). Start the UI with `../scripts/Start-DbIntelligenceWeb.ps1` (activates/installs user-scoped fnm Node if needed). Do not require admin Node installers.

## Current files

- Angular workspace (`package.json`, `angular.json`, `src/`)
- `proxy.conf.json`

## Completion report

Report changed files, assumptions, validation, unresolved risks, and required approval.
