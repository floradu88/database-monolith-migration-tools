# FindingsMigration

Template + CLI that **migrates DbIntelligence JSON mapping findings** into a **separate domain package** and scaffolds a **DataService project** from the golden **`DataServices/ShowcaseDataService`** template (`CustomerDataService` remains a thin example only).

Reads **exported JSON files** (not a database). The API’s live graph is in-memory only; always point this tool at `{repo}/.db-index/*.json` (or another export path).

Index first with DbIntelligence — see root [`HOW-TO-USE.md`](../../HOW-TO-USE.md):

```powershell
cd ..\DbIntelligence
.\scripts\Invoke-DbIntelligenceReady.ps1 "D:\path\to\repo"   # path only; fnm Node + Codegraph, no admin
.\scripts\Start-DbIntelligenceWeb.ps1                        # optional UI
```

Future roadmap: [`../../docs/FUTURE-FEATURES.md`](../../docs/FUTURE-FEATURES.md). Cutover demo: [`../DataServices/ShowcaseDataService/SHOWCASE-CUTOVER.md`](../DataServices/ShowcaseDataService/SHOWCASE-CUTOVER.md).

DbIntelligence Web can download a **promote-request** JSON (`POST /api/findings/promote`) from selected map rows — then run this CLI locally (the API never shells out).

## What it does

```text
code-to-db-map.json (+ optional stored-procedure-map.json)
  → manifests/domains/{domain}.from-findings.yml
  → manifests/migration-waves/{domain}-wave-001.from-findings.yml
  → manifests/objects/*.from-findings.yml
  → api-stubs/ (DAL hints from docs/07-data-access-strategy.md)
  → FINDINGS-REVIEW.md (AMBIGUOUS queue + data-access hints)
  → domain-package.json
  → optional Tests/*ShadowReconciliationStubTests.cs (--emit-reconciliation-tests)
  → scaffold DataServices/{Name}DataService from Showcase (PowerShell)
  → optional SQL stubs + C# Sp_* wrappers + migration-manifest.snippet.yml (generate-sp)
```

AMBIGUOUS edges are **not** packaged into ownership candidates unless you pass `-IncludeAmbiguous`.

## Incremental re-index diff

Diff two map exports and keep only **new EXTRACTED** edges for the next wave:

```powershell
dotnet run --project FindingsMigration.Cli -- diff-maps `
  --previous "...\.db-index\code-to-db-map.prev.json" `
  --current  "...\.db-index\code-to-db-map.json" `
  --out ".\out\new-extracted.json"
```

## SQL project slice (hash + ownership only)

```powershell
dotnet run --project FindingsMigration.Cli -- slice-sql `
  --objects "dbo.Customer,dbo.Order" `
  --out ".\out\Customer-sql-slice" `
  --schema customer `
  --service CustomerDataService
```

Stubs do **not** move real definitions — DBA review required before any deploy.

## PowerShell

```powershell
cd src-templates\FindingsMigration

.\scripts\Invoke-FindingsMigration.ps1 `
  -CodeToDbMap "D:\code\projects\...\.db-index\code-to-db-map.json" `
  -StoredProcedureMap "D:\code\projects\...\.db-index\stored-procedure-map.json" `
  -DomainName "Billing" `
  -OwnerTeam "TBD"

.\scripts\New-DomainFromFindings.ps1 `
  -DomainName "Billing" `
  -PackageDirectory ".\out\Billing" `
  -StoredProcedureMap "...\stored-procedure-map.json" `
  -CopyManifestsToKit
```

CLI `generate-sp` (also emits `*.migration-manifest.snippet.yml` per procedure):

```powershell
dotnet run --project FindingsMigration.Cli -- generate-sp `
  --sp-map "...\stored-procedure-map.json" `
  --service-root "..\DataServices\BillingDataService" `
  --domain Billing --service BillingDataService --schema billing
```

Package with reconciliation stubs + DAL hints:

```powershell
dotnet run --project FindingsMigration.Cli -- `
  --code-to-db "...\code-to-db-map.json" `
  --domain Billing `
  --emit-reconciliation-tests
```
