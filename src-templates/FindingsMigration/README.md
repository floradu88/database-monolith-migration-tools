# FindingsMigration

Template + CLI that **migrates DbIntelligence JSON mapping findings** into a **separate domain package** and scaffolds a **DataService project** from the golden **`DataServices/ShowcaseDataService`** template (`CustomerDataService` remains a thin example only).

Reads **exported JSON files** (not a database). The API’s live graph is in-memory only; always point this tool at `artifacts/db-intelligence/*.json` (or another export path).

Index first with DbIntelligence — see root [`HOW-TO-USE.md`](../../HOW-TO-USE.md):

```powershell
cd ..\DbIntelligence
.\scripts\Invoke-DbIntelligenceReady.ps1 "D:\path\to\repo"   # path only; fnm Node + Codegraph, no admin
.\scripts\Start-DbIntelligenceWeb.ps1                        # optional UI
```

Future roadmap: [`../../docs/FUTURE-FEATURES.md`](../../docs/FUTURE-FEATURES.md). Cutover demo: [`../DataServices/ShowcaseDataService/SHOWCASE-CUTOVER.md`](../DataServices/ShowcaseDataService/SHOWCASE-CUTOVER.md).

## What it does

```text
code-to-db-map.json (+ optional stored-procedure-map.json)
  → manifests/domains/{domain}.from-findings.yml
  → manifests/migration-waves/{domain}-wave-001.from-findings.yml
  → manifests/objects/*.from-findings.yml
  → FINDINGS-REVIEW.md (AMBIGUOUS queue)
  → domain-package.json
  → scaffold DataServices/{Name}DataService from Showcase (PowerShell)
  → optional SQL stubs + C# Sp_* wrappers (generate-sp / New-SpWrappersFromMap.ps1)
```

AMBIGUOUS edges are **not** packaged into ownership candidates unless you pass `-IncludeAmbiguous`.

## PowerShell

```powershell
cd src-templates\FindingsMigration

.\scripts\Invoke-FindingsMigration.ps1 `
  -CodeToDbMap "D:\code\projects\...\artifacts\db-intelligence\code-to-db-map.json" `
  -StoredProcedureMap "D:\code\projects\...\artifacts\db-intelligence\stored-procedure-map.json" `
  -DomainName "Insight" `
  -OwnerTeam "Personal Insight"

.\scripts\New-DomainFromFindings.ps1 `
  -DomainName "Insight" `
  -PackageDirectory ".\out\Insight" `
  -StoredProcedureMap "...\stored-procedure-map.json" `
  -CopyManifestsToKit
```

CLI `generate-sp`:

```powershell
dotnet run --project FindingsMigration.Cli -- generate-sp `
  --sp-map "...\stored-procedure-map.json" `
  --service-root "..\DataServices\InsightDataService" `
  --domain Insight --service InsightDataService --schema insight
```
