# How to use this project (PowerShell)

This kit helps you decompose a SQL Server monolith. The **runnable** local stacks today are:

- **DbIntelligence** — Codegraph + Graphify + code→SQL maps + Angular UI
- **CodegraphChat** — ChatGPT-style topic chat over a Codegraph index (single-host on `:5091`)
- **YAML Topology** — recursive `*.yaml` / `*.yml` scan → one Markdown file with a Mermaid diagram (`tools/yaml-topology`)

SQL scripts under `sql/` are for **DBA review**, not blind production execution.

All setup and run commands below are **PowerShell**. Prefer **fnm** (user-scoped Node, no admin) and Codegraph via `fnm exec --using=lts-latest` — Ready scripts do this for you.

---

## Quick start (DbIntelligence)

One command — only the project path is required (installs/checks tools without admin, builds, health-checks, starts API, indexes):

```powershell
cd D:\code\projects\database-monolith-migration-tools\src-templates\DbIntelligence

.\scripts\Invoke-DbIntelligenceReady.ps1 "D:\path\to\your\app"
```

Optional UI afterward:

```powershell
.\scripts\Start-DbIntelligenceWeb.ps1   # http://localhost:4200
```

### CodegraphChat (one command — path only)

```powershell
cd D:\code\projects\database-monolith-migration-tools\src-templates\CodegraphChat

.\scripts\Invoke-CodegraphChatReady.ps1 "D:\path\to\your\app"
```

Open http://localhost:5091/ (fnm Node + Codegraph + SPA in API wwwroot).

### YAML Topology (recursive YAML → Mermaid Markdown)

Maps a folder of `.yaml` / `.yml` files into one Markdown document with an embedded Mermaid flowchart. Uses a local Python `.venv` (PyYAML only); no admin rights.

```powershell
cd D:\code\projects\database-monolith-migration-tools\tools\yaml-topology

.\run-topology.ps1 `
  -Repo "D:\path\to\yaml-repo" `
  -Output "D:\path\to\yaml-repo\topology.md"

# Example: kit manifests (domain ↔ wave + service/DB links)
.\run-topology.ps1 `
  -Repo "..\..\manifests" `
  -Output ".\out\manifests-topology.md" `
  -Title "Kit Manifests Topology" `
  -Direction TB

# Optional: limit adapters or omit unresolved stub nodes
.\run-topology.ps1 -Repo ".\fixtures" -Output ".\out\fixtures-topology.md" -Adapters "compose,kubernetes,generic"
.\run-topology.ps1 -Repo ".\fixtures" -Output ".\out\fixtures-nostubs.md" -NoStubs
```

Full options: [`tools/yaml-topology/README.md`](tools/yaml-topology/README.md) · [`tools/yaml-topology/TOOLING.md`](tools/yaml-topology/TOOLING.md).

### Manual / stepwise (DbIntelligence)

```powershell
# From the repository root
cd D:\code\projects\database-monolith-migration-tools\src-templates\DbIntelligence

# User-scoped Node/npm (fnm, no admin) — also part of Ready/Setup
.\scripts\Initialize-DbIntelligenceNode.ps1 -Install -Yes

# One-shot kit setup only (no index): install tools, restore, build, test, health
.\scripts\Setup-DbIntelligence.ps1 -Yes

# Terminal 1 — API (http://localhost:5088)
.\scripts\Start-DbIntelligence.ps1 -Force

# Terminal 2 — Angular UI (http://localhost:4200)
.\scripts\Start-DbIntelligenceWeb.ps1

# Terminal 3 — index a repository folder
.\scripts\Invoke-DbIntelligenceIndex.ps1 -RepositoryPath "D:\path\to\your\app"

# Or batch-index every child under a parent folder
.\scripts\Invoke-DbIntelligenceBatchIndex.ps1 -ParentFolderPath "D:\code\projects"
.\scripts\Invoke-DbIntelligenceBatchIndex.ps1 -ParentFolderPath "C:\code"
```

Then open http://localhost:4200 — search the graph, filter code↔DB edges, or trigger **Index repository** from the UI.

---

## Prerequisites

| Tool | Why | Check |
|------|-----|--------|
| .NET 8 SDK | API, CLI, scanners, tests | `dotnet --list-sdks` |
| Node.js 18+ / npm | Angular UI + Codegraph npm package | `node -v` / `npm -v` |
| fnm (recommended) | User-scoped Node/npm **and** preferred Codegraph install (`fnm exec --using=lts-latest -- npm …`) | `fnm --version` |
| Python 3.10+ | Graphify + YAML Topology (local `.venv`) | `python --version` |
| `graphify` on PATH | Corpus graph | `graphify --help` |
| `codegraph` on PATH | Symbol index | `codegraph -V` |
| PowerShell 5.1+ | Setup scripts | `$PSVersionTable.PSVersion` |
| winget (Windows) | Install fnm / Python without admin elevation | `winget --version` |

Optional: SQL Server connection string (user secrets / env) only if you enable SQL inventory scan.

### Node.js without admin (fnm)

DbIntelligence PowerShell prefers **fnm** installed for the current user (`winget --scope user`), then Node LTS into your profile. **Codegraph** is installed with **fnm exec** when fnm is present (not bare system npm):

```powershell
cd src-templates\DbIntelligence

# One-shot: install fnm + Node LTS + Codegraph for this user (no elevation)
.\scripts\Initialize-DbIntelligenceNode.ps1 -Install -Yes

# Codegraph only (prefers: fnm exec --using=lts-latest -- npm i -g @colbymchenry/codegraph)
.\scripts\Initialize-DbIntelligenceNode.ps1 -InstallCodegraph -Yes

# Activate fnm Node in the *current* session (dot-source)
. .\scripts\Initialize-DbIntelligenceNode.ps1

node -v
npm -v
codegraph -V
```

`Invoke-DbIntelligenceReady.ps1`, `Invoke-CodegraphChatReady.ps1`, `Install-DbIntelligencePrereqs.ps1`, `Setup-DbIntelligence.ps1`, `Build-DbIntelligence.ps1`, `Build-CodegraphChat.ps1`, `Start-DbIntelligenceWeb.ps1`, and `Start-CodegraphChatWeb.ps1` call the Node helper / prefer `fnm exec --using=lts-latest` automatically. The C# CLI installer also prefers `fnm exec --using=lts-latest` for Codegraph when fnm is on PATH.

---

## Script catalog

All scripts live in `src-templates/DbIntelligence/scripts/`.

| Script | Purpose |
|--------|---------|
| `Invoke-DbIntelligenceReady.ps1` | **One command:** path only → prereqs (no admin) → build → health → API → index |
| `Setup-DbIntelligence.ps1` | Master: prereqs → build → test → health (no index) |
| `Initialize-DbIntelligenceNode.ps1` | User-scoped Node/npm via fnm; Codegraph via `fnm exec --using=lts-latest` when present |
| `Install-DbIntelligencePrereqs.ps1` | Node/fnm + Codegraph (`fnm exec --using=lts-latest`) + Python / pip / graphifyy / codegraph |
| `Build-DbIntelligence.ps1` | `dotnet restore/build/test` (+ optional Angular) |
| `Test-DbIntelligenceHealth.ps1` | CLI `--health` |
| `Start-DbIntelligence.ps1` | API on `:5088` (`-Force` replaces listener) |
| `Start-DbIntelligenceWeb.ps1` | Angular on `:4200` |
| `Invoke-DbIntelligenceIndex.ps1` | POST index job for a repo path (API must already be up) |
| `Invoke-DbIntelligenceBatchIndex.ps1` | Batch-index children under a parent (`D:\code\projects` or `C:\code`) |
| `Invoke-DbIntelligenceCombine.ps1` | Load all child `.db-index\graph.json` under a parent and present as **one** live graph (+ export `.db-index-combined`) |

### Setup flags

```powershell
cd src-templates\DbIntelligence

# Auto-confirm prereq installs (includes user-scoped fnm Node if needed)
.\scripts\Setup-DbIntelligence.ps1 -Yes

# Node/npm only (no admin); -Install also installs Codegraph via fnm exec --using=lts-latest when fnm exists
.\scripts\Initialize-DbIntelligenceNode.ps1 -Install -Yes
.\scripts\Initialize-DbIntelligenceNode.ps1 -InstallCodegraph -Yes

# Skip prereq installer; still build/test/health
.\scripts\Setup-DbIntelligence.ps1 -SkipPrereqs

# Skip Angular npm restore/build
.\scripts\Setup-DbIntelligence.ps1 -Yes -SkipWeb

# Build only (no Angular)
.\scripts\Build-DbIntelligence.ps1 -SkipWeb

# Build without unit tests
.\scripts\Build-DbIntelligence.ps1 -SkipWeb -SkipTests
```

### Prerequisites only

```powershell
cd src-templates\DbIntelligence

.\scripts\Install-DbIntelligencePrereqs.ps1          # interactive prompts
.\scripts\Install-DbIntelligencePrereqs.ps1 -Yes     # non-interactive

# Same installer via managed CLI
dotnet run --project .\DbIntelligence.Cli -- --health
dotnet run --project .\DbIntelligence.Cli -- --install-preqs --yes
```

What `Install-DbIntelligencePrereqs.ps1` does (Windows):

0. **Node/npm** via `Initialize-DbIntelligenceNode.ps1` (fnm + `winget --scope user`, no admin)  
1. **Codegraph** preferring **fnm** when present: `fnm exec --using=lts-latest -- npm i -g @colbymchenry/codegraph` (else PATH `npm i -g`)  
2. Then `DbIntelligence.Cli --install-preqs` (same Codegraph preference in C#):  
   - Python via `winget` if missing  
   - Ensure `pip`  
   - `python -m pip install graphifyy`  
   - Codegraph again if still missing (`fnm exec --using=lts-latest -- npm …`, else `npm`, else official install script)

### Health

```powershell
cd src-templates\DbIntelligence
.\scripts\Test-DbIntelligenceHealth.ps1

# Or hit the API once it is running
Invoke-RestMethod http://localhost:5088/api/health
Invoke-RestMethod http://localhost:5088/api/tools
```

- Healthy API → HTTP **200**
- Missing Python / Graphify / Codegraph → HTTP **503**

---

## Run (day-to-day)

### API

```powershell
cd src-templates\DbIntelligence

# Default port 5088; -Force stops whatever already listens there
.\scripts\Start-DbIntelligence.ps1 -Force

# Optional default repo for worker/startup context
.\scripts\Start-DbIntelligence.ps1 -Force -RepositoryPath "D:\code\projects\my-monolith"

# Custom port
.\scripts\Start-DbIntelligence.ps1 -Force -Port 5099
```

Equivalent without script:

```powershell
cd src-templates\DbIntelligence
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project .\DbIntelligence.Api -c Release --urls "http://localhost:5088"
```

### Angular UI

```powershell
cd src-templates\DbIntelligence
.\scripts\Start-DbIntelligenceWeb.ps1
```

Equivalent:

```powershell
# Ensure user-scoped Node is active (fnm)
. .\scripts\Initialize-DbIntelligenceNode.ps1

cd src-templates\DbIntelligence\DbIntelligence.Web
npm install
npm start
```

- UI: http://localhost:4200  
- API: http://localhost:5088  
- Dev proxy: `/api` → API

### Index a repository

```powershell
cd src-templates\DbIntelligence

.\scripts\Invoke-DbIntelligenceIndex.ps1 `
  -RepositoryPath "D:\code\projects\my-monolith" `
  -ApiBase "http://localhost:5088"
```

### Extract stored procedures (SQL inventory)

Pass a connection string (or Showcase LocalDB defaults inferred from kit `appsettings.json`) to enable read-only `runSqlScan`. Live SPs land in `GET /api/maps/stored-procedures` and `{repo}/.db-index/stored-procedure-map.json`.

**Inferred Showcase placeholders (from code — no secrets):**

| Placeholder | Inferred value |
|-------------|----------------|
| `Database:Schema` | `showcase` |
| `Database:Owned:ConnectionString` | `Server=(localdb)\mssqllocaldb;Database=ShowcaseOwned;Trusted_Connection=True;TrustServerCertificate=True` |
| `Database:SourceFacade:ConnectionString` | `Server=(localdb)\mssqllocaldb;Database=ShowcaseSource;Trusted_Connection=True;TrustServerCertificate=True` |
| SP template | `usp_Showcase_{ShowcaseReportArea}_{ShowcaseReportAction}` |
| `{ShowcaseReportArea}` | `Sales`, `Inventory` |
| `{ShowcaseReportAction}` | `Summary`, `Detail` |
| Resolved SP names | `usp_Showcase_Sales_Summary`, `usp_Showcase_Sales_Detail`, `usp_Showcase_Inventory_Summary`, `usp_Showcase_Inventory_Detail` |

Azure/Aws `CHANGE_ME` / empty `Password` stay operator-supplied (user-secrets / env) — never commit them.

```powershell
cd src-templates\DbIntelligence

# Dedicated SP extract (prints code-inferred placeholders + live map)
.\scripts\Invoke-DbIntelligenceExtractSps.ps1 -UseShowcaseLocalDefaults

# Or explicit connection (non-prod only)
.\scripts\Invoke-DbIntelligenceExtractSps.ps1 `
  -RepositoryPath "D:\code\projects\my-monolith" `
  -SqlConnectionString "Server=.;Database=Monolith;Trusted_Connection=True;TrustServerCertificate=True" `
  -SkipCodeTools

# Ready / Index also accept the same switches
.\scripts\Invoke-DbIntelligenceReady.ps1 `
  "D:\code\projects\database-monolith-migration-tools\src-templates\DataServices\ShowcaseDataService" `
  -UseShowcaseLocalDefaults -SkipBuild
```

LocalDB databases `ShowcaseOwned` / `ShowcaseSource` must exist (publish Showcase SQL project / migrations first); otherwise the SQL scan fails while code-inferred names still print.

**One-command lab publish + SP export assert:**

```powershell
cd src-templates\DataServices\ShowcaseDataService
.\scripts\Initialize-ShowcaseLocalDb.ps1
```

### Export all stored procedures to a .sql file

Read-only dump of `sys.procedures` definitions to a full output path:

```powershell
cd src-templates\DbIntelligence

.\scripts\Export-DatabaseStoredProcedures.ps1 `
  -OutputFile "D:\exports\ShowcaseOwned-procedures.sql" `
  -UseShowcaseLocalDefaults

.\scripts\Export-DatabaseStoredProcedures.ps1 `
  -OutputFile "D:\exports\monolith-sps.sql" `
  -SqlConnectionString "Server=.;Database=Monolith;Trusted_Connection=True;TrustServerCertificate=True"

# Names only
.\scripts\Export-DatabaseStoredProcedures.ps1 `
  -OutputFile "D:\exports\sp-list.txt" `
  -SqlConnectionString "Server=.;Database=Monolith;Trusted_Connection=True;TrustServerCertificate=True" `
  -ListOnly
```

Raw PowerShell (same job):

```powershell
$body = @{
  targetRepositoryPath = "D:\code\projects\my-monolith"
  runCodegraph         = $true
  runGraphify          = $true
  runRepositoryScan    = $true
  runSqlScan           = $false
} | ConvertTo-Json

$job = Invoke-RestMethod `
  -Uri "http://localhost:5088/api/index/jobs" `
  -Method Post `
  -Body $body `
  -ContentType "application/json"

do {
  Start-Sleep -Seconds 2
  $status = Invoke-RestMethod "http://localhost:5088/api/index/jobs/$($job.id)"
  Write-Host "[$($status.status)] $($status.phase) $($status.message)"
} while ($status.status -in @("Pending", "Running"))

Invoke-RestMethod "http://localhost:5088/api/maps/code-to-db"
Invoke-RestMethod "http://localhost:5088/api/maps/stored-procedures"
```

### What an index job does

Against the folder you pass:

1. Verify Python / Graphify / Codegraph  
2. `codegraph` init/sync on that path  
3. `graphify extract <path> --code-only` → import `graphify-out/graph.json`  
4. Roslyn scan for SQL / stored-procedure usage  
5. Optional SQL Server inventory (`runSqlScan`)  
6. Merge + export artifacts  

Typical artifacts (under `{repo}/.db-index/` by default):

- `graph.json`
- `code-to-db-map.json` (includes `references[]` with full path + line)
- `stored-procedure-map.json` (includes caller `references[]`)
- `code-reference-locations.json` (flat list: `fullPath`, `line`, `location`)
- `GRAPH_REPORT.md` (Mermaid overview)
- `findings.html` (standalone HTML)

---

## Useful API endpoints

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/api/health` | Prereq health |
| GET | `/api/tools` | Detailed tool report |
| POST | `/api/index/jobs` | Start index |
| GET | `/api/index/jobs/{id}` | Job status |
| GET | `/api/search?q=` | Search |
| GET | `/api/explore?q=` | Neighborhood |
| GET | `/api/graphs/unified` | Unified graph |
| GET | `/api/maps/code-to-db` | Code→DB map (+ `references[]`) |
| GET | `/api/maps/code-references` | Flat full-path + line list |
| GET | `/api/maps/stored-procedures` | SP map (+ caller `references[]`) |
| POST | `/api/export` | Write artifacts |

---

## Configuration

`DbIntelligence.Api` / Worker `appsettings.json` (placeholders only — no production secrets):

```json
{
  "DbIntelligence": {
    "TargetRepositoryPath": "",
    "ArtifactsDirectory": ".db-index",
    "CodegraphExecutable": "codegraph",
    "GraphifyExecutable": "graphify",
    "SqlConnectionString": "",
    "ProcessTimeoutSeconds": 300
  }
}
```

Optional env overrides (PowerShell):

```powershell
$env:DbIntelligence__TargetRepositoryPath = "D:\code\projects\my-monolith"
$env:DbIntelligence__SqlConnectionString = ""   # set via user-secrets in real work
$env:DbIntelligence__CodegraphExecutable = "codegraph"
$env:DbIntelligence__GraphifyExecutable = "graphify"
```

User secrets (API project):

```powershell
cd src-templates\DbIntelligence\DbIntelligence.Api
dotnet user-secrets init
dotnet user-secrets set "DbIntelligence:SqlConnectionString" "Server=(localdb)\mssqllocaldb;Database=ShowcaseOwned;Trusted_Connection=True;TrustServerCertificate=True"
```

---

## Solution layout (DbIntelligence)

Open `src-templates/DatabaseModernization.sln`.

```text
src-templates/DbIntelligence/
  DbIntelligence.Api/              # HTTP API (:5088)
  DbIntelligence.Cli/              # --health / --install-preqs
  DbIntelligence.Contracts/
  DbIntelligence.Domain/
  DbIntelligence.Infrastructure/   # Codegraph/Graphify runners, merge, store
  DbIntelligence.RepositoryScanner/
  DbIntelligence.SqlScanner/
  DbIntelligence.Worker/
  DbIntelligence.Web/              # Angular + vis-network (:4200)
  DbIntelligence.Tests/
  scripts/                         # PowerShell setup/run helpers
  README.md
```

---

## CodegraphChat (topic chat — one command)

Path only. Uses the same **fnm** (no-admin Node) + `fnm exec --using=lts-latest` Codegraph tricks as DbIntelligence. Builds the SPA into `Api/wwwroot` and starts a single-host UI.

```powershell
cd D:\code\projects\database-monolith-migration-tools\src-templates\CodegraphChat

.\scripts\Invoke-CodegraphChatReady.ps1 "D:\path\to\your\app"
```

Open **http://localhost:5091/**

| Script | Purpose |
|--------|---------|
| `Invoke-CodegraphChatReady.ps1` | One command: fnm + Codegraph + build + start |
| `Setup-CodegraphChat.ps1` | Prereqs + build only (`-Yes`) |
| `Build-CodegraphChat.ps1` | Restore/build/test; Angular via fnm → `Api/wwwroot` |
| `Start-CodegraphChat.ps1` | API only (`-Force`, `-RepositoryPath`) |
| `Start-CodegraphChatWeb.ps1` | Optional Angular hot reload `:4201` (fnm npm) |

Details: [`src-templates/CodegraphChat/README.md`](src-templates/CodegraphChat/README.md).

---

## Rest of the kit (not auto-run)

| Area | How to use |
|------|------------|
| `docs/` | Start with `docs/00-master-plan.md` and the numbered canonical docs in the root README |
| `sql/` | Review with a DBA; never execute destructive scripts blindly |
| `manifests/` | Copy/adapt domain + wave examples for your org |
| `src-templates/SourceMonolith/` | Scaffold for splitting the current DB project |
| `src-templates/DataServices/` | Example target service shape |
| `src-templates/CodegraphChat/` | Topic chat over an existing Codegraph index |
| `src-templates/MigrationControlPlane/` | Cutover/control-plane templates |
| `checklists/` | Cutover / source-split checklists |
| `validation/` | Checksums after material file changes |

Open the solution:

```powershell
cd D:\code\projects\database-monolith-migration-tools
start src-templates\DatabaseModernization.sln
# or
dotnet sln src-templates\DatabaseModernization.sln list
```

Restore/build the full solution (includes DbIntelligence):

```powershell
dotnet restore src-templates\DatabaseModernization.sln
dotnet build src-templates\DatabaseModernization.sln -c Release
dotnet test src-templates\DbIntelligence\DbIntelligence.Tests\DbIntelligence.Tests.csproj -c Release
dotnet test src-templates\FindingsMigration\FindingsMigration.Tests\FindingsMigration.Tests.csproj -c Release
dotnet test src-templates\CodegraphChat\CodegraphChat.Tests\CodegraphChat.Tests.csproj -c Release
dotnet test src-templates\Tests\Reconciliation.Tests\Reconciliation.Tests.csproj -c Release
```

---

## Storage model (current)

DbIntelligence keeps the unified graph and job list **in memory** for the API process. Maps are also written as **JSON/MD/HTML files** under `{repo}/.db-index/` on export/index. There is **no mapping database** yet — FindingsMigration and offline tools consume those JSON files.

Re-index after API restart to restore the live API graph.

After DbIntelligence exports maps under `{repo}/.db-index/`:

```powershell
cd src-templates\FindingsMigration

.\scripts\Invoke-FindingsMigration.ps1 `
  -CodeToDbMap "D:\code\projects\personalinsightanalysis\.db-index\code-to-db-map.json" `
  -StoredProcedureMap "D:\code\projects\personalinsightanalysis\.db-index\stored-procedure-map.json" `
  -DomainName "Insight" `
  -OwnerTeam "Personal Insight"

.\scripts\New-DomainFromFindings.ps1 `
  -DomainName "Insight" `
  -PackageDirectory ".\out\Insight" `
  -StoredProcedureMap "D:\code\projects\personalinsightanalysis\.db-index\stored-procedure-map.json" `
  -CopyManifestsToKit
```

Produces draft manifests + optional `DataServices\InsightDataService` scaffold from the **ShowcaseDataService** golden template (SP stubs/wrappers when a SP map is provided). `CustomerDataService` remains a thin example only. Review `FINDINGS-REVIEW.md` before ownership approval.

Additional CLI (see FindingsMigration README):

```powershell
dotnet run --project FindingsMigration.Cli -- diff-maps --previous prev.json --current curr.json --out new.json
dotnet run --project FindingsMigration.Cli -- slice-sql --objects "dbo.Customer,dbo.Order" --out .\out\slice --schema customer --service CustomerDataService
dotnet run --project FindingsMigration.Cli -- --code-to-db map.json --domain Insight --emit-reconciliation-tests
```

### SP dependency hierarchy (table/column usage per stored procedure)

Analyze which tables, columns, views, types, and functions a stored procedure (and its sub-SPs) depend on:

```powershell
cd src-templates\FindingsMigration

# Extract inventory from a live SQL Server database
.\scripts\Export-SpDependencyInventory.ps1 `
  -SpName "dbo.usp_GetCustomerSummary" `
  -OutputFile ".\out\inventory.json" `
  -SqlConnectionString "Server=.;Database=Monolith;Trusted_Connection=True;TrustServerCertificate=True"

# One-step: auto-extract + analyze (tree output)
.\scripts\Get-SpHierarchy.ps1 `
  -StoredProcedureMap "...\stored-procedure-map.json" `
  -SpName "dbo.usp_GetCustomerSummary" `
  -SqlConnectionString "Server=.;Database=Monolith;Trusted_Connection=True;TrustServerCertificate=True" `
  -Format tree

# Without database (SP-map-only fallback — no column-level detail)
.\scripts\Get-SpHierarchy.ps1 `
  -StoredProcedureMap "...\stored-procedure-map.json" `
  -SpName "dbo.usp_GetCustomerSummary" `
  -Format tree

# CLI directly
dotnet run --project FindingsMigration.Cli -- sp-hierarchy `
  --sp-map "...\stored-procedure-map.json" `
  --sp-name "dbo.usp_GetCustomerSummary" `
  --inventory ".\out\inventory.json" `
  --format tree --out ".\out\hierarchy.txt"
```

SQL script (`sql/common/50-sp-dependency-hierarchy.sql`) is read-only catalog access — review before executing. See [`src-templates/FindingsMigration/README.md`](src-templates/FindingsMigration/README.md) for the inventory JSON contract and example tree output.

Owner blue/green demo: [`src-templates/DataServices/ShowcaseDataService/SHOWCASE-CUTOVER.md`](src-templates/DataServices/ShowcaseDataService/SHOWCASE-CUTOVER.md).

```powershell
cd src-templates\DataServices\ShowcaseDataService\deploy
docker compose --profile blue --profile green up --build
# Blue :5081 · Green :5082 · dashboard at /
helm template showcase .\helm\showcase-dataservice --set ingress.greenWeight=20
```

Also run Showcase tests:

```powershell
dotnet test src-templates\DataServices\ShowcaseDataService\ShowcaseDataService.Tests\ShowcaseDataService.Tests.csproj -c Release
dotnet test src-templates\Tests\Reconciliation.Tests\Reconciliation.Tests.csproj -c Release
```

dbo → core parallel-write quality window (same database, **SP writes only** into core, dbo extras expected, evidence-only mismatches):

```powershell
cd src-templates\FindingsMigration
.\scripts\New-DboCoreDualWriteFromMap.ps1 `
  -StoredProcedureMap "...\stored-procedure-map.json" `
  -ServiceRoot "..\DataServices\InsightDataService" `
  -DomainName Insight `
  -ServiceName InsightDataService
# Or: generate-sp --parallel-dbo-core --source-schema dbo --owned-schema core
# DBA: sql/common/40-create-core-schema.sql through 45-dual-write-rbac.sql
# Checklist: checklists/dbo-to-core-sp-quality.md
# Demo: X-Data-Access-Route: ParallelWrite · POST /api/showcase/work-items · GET /api/showcase/work-items/integrity
```

Roadmap: [`docs/FUTURE-FEATURES.md`](docs/FUTURE-FEATURES.md).

### Batch: parent folder of projects

Each immediate child folder is treated as one project; analyzed one-by-one; artifacts written to **that project's `.db-index/`** (`graph.json`, `code-to-db-map.json`, `stored-procedure-map.json`, `code-reference-locations.json`, `GRAPH_REPORT.md`, `findings.html`). Parent gets `db-intelligence-batch-summary.json`. After batch (or anytime JSON exists), **combine** loads every child `.db-index\graph.json` into one live API graph and writes `{parent}\.db-index-combined\`.

```powershell
.\scripts\Invoke-DbIntelligenceBatchIndex.ps1 -ParentFolderPath "D:\code\projects"
.\scripts\Invoke-DbIntelligenceCombine.ps1 -ParentFolderPath "D:\code\projects"
# UI: Parent folder → Present all as one
```

Supported layouts (same commands — swap the parent path):

| Parent folder | Typical use |
|---------------|-------------|
| `D:\code\projects` | Projects already under the D: drive layout |
| `C:\code` | Alternate root: one project per child folder under `C:\code` |

```powershell
cd src-templates\DbIntelligence
.\scripts\Start-DbIntelligence.ps1 -Force   # if API not running

# D:\code\projects
.\scripts\Invoke-DbIntelligenceBatchIndex.ps1 -ParentFolderPath "D:\code\projects"

# C:\code (create first if missing: New-Item -ItemType Directory -Force -Path "C:\code")
.\scripts\Invoke-DbIntelligenceBatchIndex.ps1 -ParentFolderPath "C:\code"

# Optional: only folders that look like code projects
.\scripts\Invoke-DbIntelligenceBatchIndex.ps1 `
  -ParentFolderPath "D:\code\projects" `
  -RequireProjectMarkers

.\scripts\Invoke-DbIntelligenceBatchIndex.ps1 `
  -ParentFolderPath "C:\code" `
  -RequireProjectMarkers
```

API:

```powershell
Invoke-RestMethod "http://localhost:5088/api/index/discover?parentFolderPath=D:\code\projects"
Invoke-RestMethod "http://localhost:5088/api/index/discover?parentFolderPath=C:\code"

$body = @{ parentFolderPath = "D:\code\projects"; artifactsRelativeDirectory = "" } | ConvertTo-Json
Invoke-RestMethod "http://localhost:5088/api/index/batch" -Method Post -Body $body -ContentType "application/json"

$body = @{ parentFolderPath = "C:\code"; artifactsRelativeDirectory = "" } | ConvertTo-Json
Invoke-RestMethod "http://localhost:5088/api/index/batch" -Method Post -Body $body -ContentType "application/json"
```

Live API graph remains **in memory** (last completed project). Durable results are on disk per project.

---

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| Port 5088 in use / DLL locked | `.\scripts\Start-DbIntelligence.ps1 -Force` or stop `DbIntelligence.Api` before `Build-DbIntelligence.ps1` |
| Health unhealthy | `.\scripts\Install-DbIntelligencePrereqs.ps1 -Yes` then reopen the terminal so PATH refreshes |
| `node` / `npm` missing (no admin) | `.\scripts\Initialize-DbIntelligenceNode.ps1 -Install -Yes` then `. .\scripts\Initialize-DbIntelligenceNode.ps1` or open a new shell |
| `codegraph` missing | Prefer `fnm exec --using=lts-latest -- npm i -g @colbymchenry/codegraph` or `.\scripts\Initialize-DbIntelligenceNode.ps1 -InstallCodegraph -Yes` |
| `fnm exec` → "Can't find version in dotfiles" | Use `--using=lts-latest`, work under `src-templates/DbIntelligence` (ships `.node-version`), or run `.\scripts\Initialize-DbIntelligenceNode.ps1 -Install -Yes` |
| `Start-DbIntelligenceWeb.ps1` parse error (`string` terminator / missing `}`) | Pull latest scripts (ASCII hyphens only). Unicode em dashes in `.ps1` break Windows PowerShell 5.1 |
| `graphify` not found / wrong CLI | Install package `graphifyy`; help text must mention `extract` / `graphify-out` |
| Index fails on Graphify JSON | Ensure you are on the fixed importer (numeric `community`, `links` edges) — rebuild API and restart with `-Force` |
| Angular cannot reach API | Start API first; confirm http://localhost:5088/api/health |
| SQL scan skipped | Expected when `runSqlScan` is `$false` or connection string empty |

---

## Safety

- Prefer additive, reversible changes.
- Do not invent credentials or production values.
- Never auto-run destructive SQL from this kit.
- SqlScanner is **read-only** inventory.
- Dynamic/interpolated SQL edges are `AMBIGUOUS` and need human review.

---

## More detail

- DbIntelligence project README: [`src-templates/DbIntelligence/README.md`](src-templates/DbIntelligence/README.md)
- Codegraph topic chat: [`src-templates/CodegraphChat/README.md`](src-templates/CodegraphChat/README.md)
- Findings → domain template: [`src-templates/FindingsMigration/README.md`](src-templates/FindingsMigration/README.md)
- YAML topology (Mermaid Markdown): [`tools/yaml-topology/README.md`](tools/yaml-topology/README.md)
- Future features: [`docs/FUTURE-FEATURES.md`](docs/FUTURE-FEATURES.md)
- Discovery model: [`docs/03-discovery-and-ai-indexing.md`](docs/03-discovery-and-ai-indexing.md)
- Review status: [`REVIEW-REPORT.md`](REVIEW-REPORT.md)
- Agent entry: [`AGENTS.md`](AGENTS.md)
