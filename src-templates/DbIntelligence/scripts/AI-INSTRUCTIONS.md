# AI Instructions — `src-templates/DbIntelligence/scripts`

## Purpose

PowerShell setup/run helpers for local DbIntelligence development.

## Scripts

- `Setup-DbIntelligence.ps1` — prereqs + build + test + health
- `Initialize-DbIntelligenceNode.ps1` — user-scoped Node/npm via fnm; Codegraph via `fnm exec -- npm i -g` when fnm present
- `Install-DbIntelligencePrereqs.ps1` — Node/fnm/Codegraph first, then `--install-preqs` wrapper
- `Build-DbIntelligence.ps1` — restore/build/test (+ optional Angular); stops port-5088 lockers when possible
- `Test-DbIntelligenceHealth.ps1` — CLI `--health`
- `Start-DbIntelligence.ps1` — API on :5088 (`-Force`, `-Port`, `-RepositoryPath`)
- `Start-DbIntelligenceWeb.ps1` — Angular on :4200 (activates/installs fnm Node if needed)
- `Invoke-DbIntelligenceIndex.ps1` — POST index job against a repo path
- `Invoke-DbIntelligenceBatchIndex.ps1` — parent folder → index each child project; results in each project root

Human docs: root `HOW-TO-USE.md` and parent `DbIntelligence/README.md` (PowerShell-first).

## Mandatory workflow

1. Prefer these scripts over inventing one-off install steps.
2. Prefer user-scoped Node via `Initialize-DbIntelligenceNode.ps1` (fnm + winget `--scope user`) over admin Node installers.
3. Prefer Codegraph install via `fnm exec -- npm i -g @colbymchenry/codegraph` whenever fnm is present; bare `npm i -g` only as fallback.
4. Do not hard-code production credentials or repo paths.
5. Keep prompts interactive unless `-Yes` is passed.
