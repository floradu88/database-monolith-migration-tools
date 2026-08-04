# AI Instructions — `src-templates/DbIntelligence/scripts`

## Purpose

PowerShell setup/run helpers for local DbIntelligence development.

## Scripts

- `Invoke-DbIntelligenceReady.ps1` — **one command:** project path only → prereqs (no admin) → build → health → API → index (`-SqlConnectionString` / `-UseShowcaseLocalDefaults` enables SQL SP scan)
- `Setup-DbIntelligence.ps1` — prereqs + build + test + health
- `Initialize-DbIntelligenceNode.ps1` — user-scoped Node/npm via fnm; Codegraph via `fnm exec --using=lts-latest -- npm i -g` when fnm present
- `Install-DbIntelligencePrereqs.ps1` — Node/fnm/Codegraph first, then `--install-preqs` wrapper
- `Build-DbIntelligence.ps1` — restore/build/test (+ optional Angular); stops port-5088 lockers when possible
- `Test-DbIntelligenceHealth.ps1` — CLI `--health`
- `Start-DbIntelligence.ps1` — API on :5088 (`-Force`, `-Port`, `-RepositoryPath`)
- `Start-DbIntelligenceWeb.ps1` — Angular on :4200 (activates/installs fnm Node if needed)
- `Invoke-DbIntelligenceIndex.ps1` — POST index job against a repo path (`-SqlConnectionString` / `-UseShowcaseLocalDefaults`)
- `Invoke-DbIntelligenceExtractSps.ps1` — SP extract: resolve CS → `runSqlScan` → print live map + code-inferred Showcase placeholders
- `Export-DatabaseStoredProcedures.ps1` — write all SP definitions (or name list) from a DB to a full `-OutputFile` path (read-only)
- `Resolve-DbIntelligenceSqlConnection.ps1` — shared CS resolver + `Get-ShowcaseProcedurePlaceholders`
- `Invoke-DbIntelligenceBatchIndex.ps1` — parent folder → index each child project; results in each project root
- `Invoke-DbIntelligenceCombine.ps1` — load all child `.db-index\graph.json` under a parent into **one** live graph (+ `.db-index-combined` export)

Human docs: root `HOW-TO-USE.md` and parent `DbIntelligence/README.md` (PowerShell-first).

## Mandatory workflow

1. Prefer `Invoke-DbIntelligenceReady.ps1 "<project-path>"` when the operator only has a repo path to supply.
2. Prefer these scripts over inventing one-off install steps.
3. Prefer user-scoped Node via `Initialize-DbIntelligenceNode.ps1` (fnm + winget `--scope user`) over admin Node installers.
4. Prefer Codegraph install via `fnm exec --using=lts-latest -- npm i -g @colbymchenry/codegraph` whenever fnm is present; bare `npm i -g` only as fallback.
5. Do not hard-code production credentials or repo paths.
6. Keep prompts interactive unless `-Yes` is passed (Ready always uses auto-yes).
