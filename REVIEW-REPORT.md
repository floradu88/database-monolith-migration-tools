# V5 Review Report

## Review result

The kit is structurally complete as an implementation and planning starter, subject to the platform decision and production-specific values listed below.

## Corrections made in V5

- Added missing catalog tables referenced by reports: `inventory.Application`, `telemetry.DatabaseObjectUsageHourly`, and `ownership.ObjectOwnership`.
- Added an hourly usage upsert contract.
- Added DDL and permission-change audit guidance.
- Added missing project scaffolds for DB Intelligence, Migration Control Plane, Building Blocks, tests, all source-monolith domains, and the complete example data service.
- Removed hard-coded preview versions from new SQL project scaffolds and added a version policy.
- Added explicit EF separate-migrations-project requirements.
- Added canonical versus supplemental document navigation.
- Added a required platform decision record.
- Regenerated AI instructions for every folder.
- Added machine-readable validation output and checksums.

## Repository integration (post-V5)

- Imported the reviewed kit into this git repository while preserving the zip layout.
- Added Cursor project rules under `.cursor/rules/` and Claude/shared agent entrypoints (`CLAUDE.md`, `AGENTS.md`).
- Updated the root README and AI instruction index for Cursor + Claude Code usage.
- Regenerated validation checksums and summary after agent-tooling files were added.
- Implemented DbIntelligence (.NET 8): Codegraph/Graphify CLI orchestration, Roslyn code→SQL/SP scanning, Graphify-shaped JSON exports, ASP.NET API, and Angular/vis-network UI.
- Added PowerShell setup/run scripts under `src-templates/DbIntelligence/scripts/` and root [`HOW-TO-USE.md`](HOW-TO-USE.md) (all local commands in PowerShell).
- Added user-scoped Node/npm provisioning via **fnm** (`Initialize-DbIntelligenceNode.ps1`, `winget --scope user` — no admin); wired into Setup / Prereqs / Build / Start-Web.
- Codegraph install prefers **fnm** when present (`fnm exec --using=lts-latest -- npm i -g @colbymchenry/codegraph`) in both PowerShell and `DbIntelligence.Cli --install-preqs`; bare npm / official script are fallbacks.
- Graphify importer accepts NetworkX `links` and numeric `community` fields from `graphify extract … --code-only`.
- Added `FindingsMigration` template: package JSON maps into draft domain manifests + scaffold DataService from Customer template (`docs/FUTURE-FEATURES.md`).
- Added parent-folder batch indexing: each child project analyzed sequentially with artifacts written to that project's root (`Invoke-DbIntelligenceBatchIndex.ps1`, `/api/index/batch`); documented for `D:\code\projects` and `C:\code`.
- Documented in-memory map storage (no catalog DB yet); durable output remains JSON/MD export.
- Root README expanded with full PowerShell command reference (setup, index, batch, findings, solution build).

## Still requires environment-specific decisions

- exact SQL hosting model;
- production retention periods;
- audit destination and permissions;
- approved SDK/package versions;
- domain list and actual ownership;
- migration synchronization mechanism;
- SLO/RPO/RTO values;
- capacity and shard thresholds;
- deployment platform and identity model.

## Readiness classification

- Architecture and planning: ready.
- Repository/project scaffolding: ready as templates.
- SQL scripts: ready for DBA review, not blind production execution.
- Production deployment: blocked until platform ADR, permissions, retention, and environment values are approved.
