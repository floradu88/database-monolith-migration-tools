# How to use this project (PowerShell)

This kit helps you decompose a SQL Server monolith. The **runnable** local stack today is **DbIntelligence** (Codegraph + Graphify + code→SQL maps + Angular UI). SQL scripts under `sql/` are for **DBA review**, not blind production execution.

All setup and run commands below are **PowerShell**.

---

## Quick start (DbIntelligence)

```powershell
# From the repository root
cd D:\code\projects\database-monolith-migration-tools\src-templates\DbIntelligence

# User-scoped Node/npm (fnm, no admin) — also part of Setup
.\scripts\Initialize-DbIntelligenceNode.ps1 -Install -Yes

# One-shot: install tools (prompts), restore, build, test, health
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
| Node.js 18+ / npm | Angular UI (+ optional `npm i -g` codegraph) | `node -v` / `npm -v` |
| fnm (recommended) | User-scoped Node/npm **without admin** | `fnm --version` |
| Python 3.10+ | Graphify | `python --version` |
| `graphify` on PATH | Corpus graph | `graphify --help` |
| `codegraph` on PATH | Symbol index | `codegraph -V` |
| PowerShell 5.1+ | Setup scripts | `$PSVersionTable.PSVersion` |
| winget (Windows) | Install fnm / Python without admin elevation | `winget --version` |

Optional: SQL Server connection string (user secrets / env) only if you enable SQL inventory scan.

### Node.js without admin (fnm)

DbIntelligence PowerShell prefers **fnm** installed for the current user (`winget --scope user`), then Node LTS into your profile:

```powershell
cd src-templates\DbIntelligence

# One-shot: install fnm + Node LTS for this user (no elevation)
.\scripts\Initialize-DbIntelligenceNode.ps1 -Install -Yes

# Activate fnm Node in the *current* session (dot-source)
. .\scripts\Initialize-DbIntelligenceNode.ps1

node -v
npm -v
```

`Install-DbIntelligencePrereqs.ps1`, `Setup-DbIntelligence.ps1`, `Build-DbIntelligence.ps1`, and `Start-DbIntelligenceWeb.ps1` call this helper automatically.

---

## Script catalog

All scripts live in `src-templates/DbIntelligence/scripts/`.

| Script | Purpose |
|--------|---------|
| `Setup-DbIntelligence.ps1` | Master: prereqs → build → test → health |
| `Initialize-DbIntelligenceNode.ps1` | User-scoped Node/npm via fnm (no admin) |
| `Install-DbIntelligencePrereqs.ps1` | Node/fnm + Python / pip / graphifyy / codegraph |
| `Build-DbIntelligence.ps1` | `dotnet restore/build/test` (+ optional Angular) |
| `Test-DbIntelligenceHealth.ps1` | CLI `--health` |
| `Start-DbIntelligence.ps1` | API on `:5088` (`-Force` replaces listener) |
| `Start-DbIntelligenceWeb.ps1` | Angular on `:4200` |
| `Invoke-DbIntelligenceIndex.ps1` | POST index job for a repo path |
| `Invoke-DbIntelligenceBatchIndex.ps1` | Batch-index children under a parent (`D:\code\projects` or `C:\code`) |

### Setup flags

```powershell
cd src-templates\DbIntelligence

# Auto-confirm prereq installs (includes user-scoped fnm Node if needed)
.\scripts\Setup-DbIntelligence.ps1 -Yes

# Node/npm only (no admin)
.\scripts\Initialize-DbIntelligenceNode.ps1 -Install -Yes

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
1. Then `DbIntelligence.Cli --install-preqs`:  
   - Python via `winget` if missing  
   - Ensure `pip`  
   - `python -m pip install graphifyy`  
   - `npm i -g @colbymchenry/codegraph` (fallback install script) — uses the fnm Node just activated when possible

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

Typical artifacts (under the configured artifacts directory, often under the target repo):

- `graph.json`
- `code-to-db-map.json`
- `stored-procedure-map.json`
- `GRAPH_REPORT.md`

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
| GET | `/api/maps/code-to-db` | Code→DB map |
| GET | `/api/maps/stored-procedures` | SP map |
| POST | `/api/export` | Write artifacts |

---

## Configuration

`DbIntelligence.Api` / Worker `appsettings.json` (placeholders only — no production secrets):

```json
{
  "DbIntelligence": {
    "TargetRepositoryPath": "",
    "ArtifactsDirectory": "artifacts/db-intelligence",
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
dotnet user-secrets set "DbIntelligence:SqlConnectionString" "Server=.;Database=Monolith;Trusted_Connection=True;TrustServerCertificate=True"
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

## Rest of the kit (not auto-run)

| Area | How to use |
|------|------------|
| `docs/` | Start with `docs/00-master-plan.md` and the numbered canonical docs in the root README |
| `sql/` | Review with a DBA; never execute destructive scripts blindly |
| `manifests/` | Copy/adapt domain + wave examples for your org |
| `src-templates/SourceMonolith/` | Scaffold for splitting the current DB project |
| `src-templates/DataServices/` | Example target service shape |
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
```

---

## Storage model (current)

DbIntelligence keeps the unified graph and job list **in memory** for the API process. Maps are also written as **JSON files** under `artifacts/db-intelligence/` on export/index. There is **no mapping database** yet — FindingsMigration and offline tools consume those JSON files.

Re-index after API restart to restore the live API graph.

After DbIntelligence exports maps under `artifacts/db-intelligence/`:

```powershell
cd src-templates\FindingsMigration

.\scripts\Invoke-FindingsMigration.ps1 `
  -CodeToDbMap "D:\code\projects\personalinsightanalysis\artifacts\db-intelligence\code-to-db-map.json" `
  -StoredProcedureMap "D:\code\projects\personalinsightanalysis\artifacts\db-intelligence\stored-procedure-map.json" `
  -DomainName "Insight" `
  -OwnerTeam "Personal Insight"

.\scripts\New-DomainFromFindings.ps1 `
  -DomainName "Insight" `
  -PackageDirectory ".\out\Insight" `
  -CopyManifestsToKit
```

Produces draft manifests + optional `DataServices\InsightDataService` scaffold from the Customer template. Review `FINDINGS-REVIEW.md` before ownership approval.

Roadmap: [`docs/FUTURE-FEATURES.md`](docs/FUTURE-FEATURES.md).

### Batch: parent folder of projects

Each immediate child folder is treated as one project; analyzed one-by-one; artifacts written to **that project's root** (`graph.json`, `code-to-db-map.json`, `stored-procedure-map.json`, `GRAPH_REPORT.md`). Parent gets `db-intelligence-batch-summary.json`.

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
- Findings → domain template: [`src-templates/FindingsMigration/README.md`](src-templates/FindingsMigration/README.md)
- Future features: [`docs/FUTURE-FEATURES.md`](docs/FUTURE-FEATURES.md)
- Discovery model: [`docs/03-discovery-and-ai-indexing.md`](docs/03-discovery-and-ai-indexing.md)
- Review status: [`REVIEW-REPORT.md`](REVIEW-REPORT.md)
- Agent entry: [`AGENTS.md`](AGENTS.md)
