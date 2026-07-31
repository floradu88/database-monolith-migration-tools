# AI Instructions — `src-templates/FindingsMigration`

## Purpose

Turn DbIntelligence JSON maps into draft domain manifests and scaffold DataService projects from the Customer template. Does not approve ownership or run production SQL.

## Mandatory workflow

1. Read `README.md` and `docs/FUTURE-FEATURES.md`.
2. Prefer PowerShell scripts under `scripts/` for operator flows.
3. Never treat AMBIGUOUS findings as owned without human review.
4. Keep SQL project ownership and EF migrations ownership non-overlapping in generated YAML.
5. Prefer additive changes; do not silently overwrite kit `manifests/` without `-CopyManifestsToKit` intent.

## Current contents

- `FindingsMigration.Contracts/`
- `FindingsMigration.Core/`
- `FindingsMigration.Cli/`
- `FindingsMigration.Tests/`
- `scripts/Invoke-FindingsMigration.ps1`
- `scripts/New-DomainFromFindings.ps1`
- `README.md`
