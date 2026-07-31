# AI Instructions — `src-templates/FindingsMigration/scripts`

## Purpose

PowerShell wrappers for packaging findings and scaffolding DataService projects.

## Scripts

- `Invoke-FindingsMigration.ps1` — run CLI against code-to-db / SP maps
- `New-DomainFromFindings.ps1` — copy `CustomerDataService` template + optional copy manifests into kit

## Rules

1. Do not invent production credentials.
2. Default: leave AMBIGUOUS findings out of ownership candidates.
3. `-CopyManifestsToKit` is explicit; do not imply silent promotion.
