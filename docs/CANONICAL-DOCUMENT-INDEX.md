# Canonical Document Index

## How to run / operate locally

- `../HOW-TO-USE.md` — PowerShell setup, run, index, batch (`D:\code\projects` / `C:\code`); prefer `Invoke-DbIntelligenceReady.ps1` (path only); fnm Node; Codegraph via `fnm exec --using=lts-latest`
- `../checklists/dbo-to-core-sp-quality.md` — dbo→core parallel-write integrity window
- `PROJECT-GUIDE.md` — kit projects, why use them, pros/cons (OnPrem/Azure/Aws hosting summary links)
- `../src-templates/DbIntelligence/README.md` — DbIntelligence project how-to
- `../src-templates/DbIntelligence/scripts/Invoke-DbIntelligenceReady.ps1` — path-only one-shot (prereqs → build → health → API → index)
- `../src-templates/DbIntelligence/scripts/Initialize-DbIntelligenceNode.ps1` — fnm Node/npm; Codegraph via `fnm exec --using=lts-latest`
- `../src-templates/FindingsMigration/README.md` — promote JSON maps to domain packages
- `../src-templates/DataServices/ShowcaseDataService/DATABASE-HOSTING.md` — OnPrem / Azure / Aws pros/cons + config
- `dbintelligence-runbook.md` — short PowerShell runbook + plan snapshot
- `FUTURE-FEATURES.md` — findings → domain project roadmap
- `03-discovery-and-ai-indexing.md` — discovery model + DbIntelligence PowerShell snippet

## Canonical plan

0. `PROJECT-GUIDE.md` — projects + pros/cons (read with the plan)
1. `00-master-plan.md`
2. `01-target-architecture.md`
3. `02-solution-and-project-structure.md`
4. `03-source-monolith-split.md`
5. `04-target-database-project-strategy.md`
6. `05-migration-control-plane.md`
7. `06-usage-tracking-and-audit.md`
8. `07-data-access-strategy.md`
9. `08-performance-monitoring-and-scaling.md`
10. `09-rbac-security-and-change-control.md`
11. `10-execution-roadmap.md`


## Supplemental references retained from earlier iterations

The following documents contain useful detail but are not the primary navigation path:

- `01-architecture.md`
- `02-project-structure.md`
- `03-discovery-and-ai-indexing.md`
- `04-usage-tracking-strategy.md`
- `05-migration-plan.md`
- `06-stored-procedure-function-retirement.md`
- `07-security-observability-nfr.md`
- `08-runbook-first-30-days.md`
- `09-data-model-and-api.md`
- `10-decisions-and-risks.md`
- `10-production-grade-gaps-and-decisions.md`
- `11-data-access-ab-evaluation.md`
- `12-monitoring-observability-and-slos.md`
- `13-scaling-read-heavy-services-and-sharding.md`
- `14-rbac-and-controlled-migrations.md`
- `15-dml-and-programmability-access-tracking.md`
- `16-production-decomposition-runbook.md`

When information differs, the canonical plan and platform-specific SQL instructions take precedence.
