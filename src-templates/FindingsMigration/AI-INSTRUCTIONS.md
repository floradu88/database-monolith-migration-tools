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
7. Agent playbook: given maps → package → `New-DomainFromFindings` (Showcase) → generate wrappers (`-ParallelDboCore` when migrating write SPs dbo→core) → wire façade (SourceFacade/Owned/Shadow/ParallelWrite) → blue+green → shadow + table-integrity evidence on dashboard → human approve.

## Current contents

- `FindingsMigration.Contracts/`
- `FindingsMigration.Core/` — DomainPackageBuilder, SpWrapperGenerator, DualWriteArtifactGenerator, CodeToDbDiffService, SqlProjectSliceGenerator, DataAccessRecommendation, ReconciliationTestStubGenerator, DomainSuggestionService, ConfidenceGateService
- `FindingsMigration.Cli/` — package, `generate-sp` (`--parallel-dbo-core`), `suggest-domains`, `confidence-gate`, `diff-maps`, `slice-sql`
- `FindingsMigration.Tests/`
- `scripts/Invoke-FindingsMigration.ps1`
- `scripts/New-DomainFromFindings.ps1`
- `scripts/New-SpWrappersFromMap.ps1`
- `scripts/New-DboCoreDualWriteFromMap.ps1`
- `README.md`

## Phase 3 packaging notes

- `diff-maps`: only NEW EXTRACTED edges between two `code-to-db-map.json` files.
- `slice-sql`: hash placeholder + ownership comment stubs only — never move real SQL definitions.
- API stubs / FINDINGS notes include one-line EF vs Dapper/SP hints from `docs/07-data-access-strategy.md`.
- `--emit-reconciliation-tests` writes Showcase-aligned xUnit stubs (Skipped until ownership approval).
- `generate-sp` also emits `*.migration-manifest.snippet.yml` per procedure (object / owner / wave placeholder).
