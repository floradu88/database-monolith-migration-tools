# AI Instructions — `src-templates/FindingsMigration`

## Purpose

Turn DbIntelligence JSON maps into draft domain manifests and scaffold DataService projects from the **ShowcaseDataService** golden template. Optionally emit SQL SP stubs + Dapper wrappers. Does not approve ownership or run production SQL.

## Mandatory workflow

1. Read `README.md`, root `HOW-TO-USE.md`, `docs/FUTURE-FEATURES.md`, and Showcase `SHOWCASE-CUTOVER.md`.
2. Prefer PowerShell scripts under `scripts/` for operator flows.
3. Never treat AMBIGUOUS findings as owned without human review.
4. Keep SQL project ownership and EF migrations ownership non-overlapping in generated YAML.
5. Prefer additive changes; do not silently overwrite kit `manifests/` without `-CopyManifestsToKit` intent.
6. Indexing prerequisites (Node/fnm, Codegraph via `fnm exec`, API) live under `../DbIntelligence/scripts/` — do not invent alternate install paths.
7. Agent playbook: given maps → package → `New-DomainFromFindings` (Showcase) → generate wrappers → wire façade (SourceFacade/Owned/Shadow) → blue+green → shadow evidence on dashboard → human approve.

## Current contents

- `FindingsMigration.Contracts/`
- `FindingsMigration.Core/` (DomainPackageBuilder, SpWrapperGenerator)
- `FindingsMigration.Cli/` (package + `generate-sp`)
- `FindingsMigration.Tests/`
- `scripts/Invoke-FindingsMigration.ps1`
- `scripts/New-DomainFromFindings.ps1`
- `scripts/New-SpWrappersFromMap.ps1`
- `README.md`
