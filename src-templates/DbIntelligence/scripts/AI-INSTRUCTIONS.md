# AI Instructions — `src-templates/DbIntelligence/scripts`

## Purpose

PowerShell setup/run helpers for local DbIntelligence development.

## Scripts

- `Setup-DbIntelligence.ps1` — prereqs + build + test + health
- `Install-DbIntelligencePrereqs.ps1` — `--install-preqs` wrapper
- `Build-DbIntelligence.ps1` — restore/build/test (+ optional Angular); stops port-5088 lockers when possible
- `Test-DbIntelligenceHealth.ps1` — CLI `--health`
- `Start-DbIntelligence.ps1` — API on :5088 (`-Force`, `-Port`, `-RepositoryPath`)
- `Start-DbIntelligenceWeb.ps1` — Angular on :4200
- `Invoke-DbIntelligenceIndex.ps1` — POST index job against a repo path
- `Invoke-DbIntelligenceBatchIndex.ps1` — parent folder → index each child project; results in each project root

Human docs: root `HOW-TO-USE.md` and parent `DbIntelligence/README.md` (PowerShell-first).

## Mandatory workflow

1. Prefer these scripts over inventing one-off install steps.
2. Do not hard-code production credentials or repo paths.
3. Keep prompts interactive unless `-Yes` is passed.
