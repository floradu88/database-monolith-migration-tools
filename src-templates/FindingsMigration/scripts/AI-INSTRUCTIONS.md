# AI Instructions — `src-templates/FindingsMigration/scripts`

## Purpose

PowerShell wrappers for packaging findings and scaffolding DataService projects.

## Scripts

- `Invoke-FindingsMigration.ps1` — run CLI against code-to-db / SP maps
- `scripts/New-DomainFromFindings.ps1` — copy Showcase golden template + optional copy manifests into kit; `-ParallelDboCore` for dbo→core clones
- `scripts/New-DboCoreDualWriteFromMap.ps1` — generate-sp `--parallel-dbo-core` wrapper

## Rules

1. Do not invent production credentials.
2. Default: leave AMBIGUOUS findings out of ownership candidates.
3. `-CopyManifestsToKit` is explicit; do not imply silent promotion.
