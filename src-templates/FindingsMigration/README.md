# FindingsMigration

Template + CLI that **migrates DbIntelligence JSON mapping findings** into a **separate domain package** and scaffolds a **DataService project** from `DataServices/CustomerDataService`.

Reads **exported JSON files** (not a database). The API’s live graph is in-memory only; always point this tool at `artifacts/db-intelligence/*.json` (or another export path).

Index first with DbIntelligence — see root [`HOW-TO-USE.md`](../../HOW-TO-USE.md):

```powershell
cd ..\DbIntelligence
.\scripts\Invoke-DbIntelligenceReady.ps1 "D:\path\to\repo"   # path only; fnm Node + Codegraph, no admin
.\scripts\Start-DbIntelligenceWeb.ps1                        # optional UI
# or batch: .\scripts\Invoke-DbIntelligenceBatchIndex.ps1 -ParentFolderPath "D:\code\projects"
#           .\scripts\Invoke-DbIntelligenceBatchIndex.ps1 -ParentFolderPath "C:\code"
```

Future roadmap: [`../../docs/FUTURE-FEATURES.md`](../../docs/FUTURE-FEATURES.md).

## What it does

```text
code-to-db-map.json (+ optional stored-procedure-map.json)
  → manifests/domains/{domain}.from-findings.yml
  → manifests/migration-waves/{domain}-wave-001.from-findings.yml
  → manifests/objects/*.from-findings.yml
  → FINDINGS-REVIEW.md (AMBIGUOUS queue)
  → domain-package.json
  → scaffold DataServices/{Name}DataService (PowerShell)
```

AMBIGUOUS edges are **not** packaged into ownership candidates unless you pass `-IncludeAmbiguous`.

## PowerShell

```powershell
cd src-templates\FindingsMigration

# 1) Package maps from a DbIntelligence export (example: Personal Insight Analysis)
#    Paths work the same under D:\code\projects\... or C:\code\...
.\scripts\Invoke-FindingsMigration.ps1 `
  -CodeToDbMap "D:\code\projects\personalinsightanalysis\artifacts\db-intelligence\code-to-db-map.json" `
  -StoredProcedureMap "D:\code\projects\personalinsightanalysis\artifacts\db-intelligence\stored-procedure-map.json" `
  -DomainName "Insight" `
  -OwnerTeam "Personal Insight"

# Same maps if the project was indexed under C:\code:
# .\scripts\Invoke-FindingsMigration.ps1 `
#   -CodeToDbMap "C:\code\personalinsightanalysis\artifacts\db-intelligence\code-to-db-map.json" `
#   -StoredProcedureMap "C:\code\personalinsightanalysis\artifacts\db-intelligence\stored-procedure-map.json" `
#   -DomainName "Insight" `
#   -OwnerTeam "Personal Insight"

# 2) Scaffold a new DataService from the Customer template
.\scripts\New-DomainFromFindings.ps1 `
  -DomainName "Insight" `
  -PackageDirectory ".\out\Insight" `
  -CopyManifestsToKit
```

CLI equivalent:

```powershell
dotnet run --project .\FindingsMigration.Cli -c Release -- `
  --code-to-db "D:\path\to\code-to-db-map.json" `
  --sp-map "D:\path\to\stored-procedure-map.json" `
  --domain Insight `
  --out .\out\Insight `
  --owner "Personal Insight"
```

## Projects

| Project | Role |
|---------|------|
| `FindingsMigration.Contracts` | Map + package DTOs |
| `FindingsMigration.Core` | Domain package builder |
| `FindingsMigration.Cli` | `findings-migrate` executable |
| `FindingsMigration.Tests` | Unit tests |
| `scripts/` | PowerShell operator entrypoints |

## Safety

- Output is **draft** (`status: draft-from-findings`, `requires_human_ownership_approval: true`).
- Does not execute SQL or invent production credentials.
- Does not overlap EF vs SQL ownership — `ef_migrations_own` starts empty.
- Review `FINDINGS-REVIEW.md` before promoting manifests.

## Future (not in v1)

See `docs/FUTURE-FEATURES.md`: community-based domain splits, CI confidence gates, SP-centric packaging, Angular “Promote to domain”, catalog DB, etc.
