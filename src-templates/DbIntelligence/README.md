# DbIntelligence — How to use (PowerShell)

.NET 8 evidence-graph stack: runs **Codegraph** and **Graphify** against a repository path, merges Roslyn code→SQL/SP scans, exports Graphify-shaped JSON, and serves an Angular graph UI.

> Full kit guide: [`../../HOW-TO-USE.md`](../../HOW-TO-USE.md)

## Storage model (current)

| What | Where |
|------|--------|
| Live graph + maps served by API | **In memory** (`FileIntelligenceStore`) for the running process |
| Index job status | **In memory** (lost on API restart) |
| Durable snapshot | **JSON/MD/HTML** under `{repo}/.db-index/` when export/index runs |

There is **no database** for mappings yet. Restarting the API clears the live graph until you re-index or load from exported files (re-index is the supported path today). A catalog DB is a future feature — see [`docs/FUTURE-FEATURES.md`](../../docs/FUTURE-FEATURES.md).

## Operating model

1. Prefer **one command** with only the project path: `Invoke-DbIntelligenceReady.ps1` (fnm Node no admin, Graphify, Codegraph, build, health, API, index).
2. Or provision tools with `Setup-DbIntelligence.ps1` / `Initialize-DbIntelligenceNode.ps1`, then start API and index separately.
3. Point DbIntelligence at a **repository folder** (or batch parent such as `D:\code\projects` / `C:\code`).

## One command (path only)

```powershell
cd src-templates\DbIntelligence
.\scripts\Invoke-DbIntelligenceReady.ps1 "D:\path\to\your\app"
```

Does: prereqs (no admin) → build → health → start API → index → print map counts.

## One-shot kit setup (no index)

```powershell
cd src-templates\DbIntelligence
.\scripts\Setup-DbIntelligence.ps1 -Yes
```

Node/npm without admin (fnm, user scope) — also run automatically by Ready/setup/prereqs/build/web scripts.
**Codegraph** installs with `fnm exec --using=lts-latest -- npm i -g @colbymchenry/codegraph` when fnm is present
(`.node-version` in this folder is `lts-latest` so bare `fnm exec` also resolves here; no admin required):

```powershell
.\scripts\Initialize-DbIntelligenceNode.ps1 -Install -Yes
.\scripts\Initialize-DbIntelligenceNode.ps1 -InstallCodegraph -Yes
. .\scripts\Initialize-DbIntelligenceNode.ps1
```

## Run (manual)

```powershell
# Terminal 1 — API http://localhost:5088
.\scripts\Start-DbIntelligence.ps1 -Force

# Terminal 2 — UI http://localhost:4200
.\scripts\Start-DbIntelligenceWeb.ps1

# Terminal 3 — index one repo
.\scripts\Invoke-DbIntelligenceIndex.ps1 -RepositoryPath "D:\path\to\repo"

# Parent folder: each subfolder is a project; results written to each project root
.\scripts\Invoke-DbIntelligenceBatchIndex.ps1 -ParentFolderPath "D:\code\projects"
.\scripts\Invoke-DbIntelligenceBatchIndex.ps1 -ParentFolderPath "C:\code"   # alternate root
```

## Scripts

| Script | Purpose |
|--------|---------|
| `Invoke-DbIntelligenceReady.ps1` | **One command:** path only → prereqs → build → health → API → index |
| `Setup-DbIntelligence.ps1` | Prereqs + build + test + health (no index) |
| `Initialize-DbIntelligenceNode.ps1` | User-scoped Node/npm via fnm; Codegraph via `fnm exec --using=lts-latest` when present |
| `Install-DbIntelligencePrereqs.ps1` | Node/fnm + Codegraph (`fnm exec --using=lts-latest`) + Python / pip / graphifyy (`-Yes`) |
| `Build-DbIntelligence.ps1` | Restore / build / test (`-SkipWeb`, `-SkipTests`) |
| `Test-DbIntelligenceHealth.ps1` | CLI health |
| `Start-DbIntelligence.ps1` | API (`-Force`, `-Port`, `-RepositoryPath`) |
| `Start-DbIntelligenceWeb.ps1` | Angular (activates/installs fnm Node if needed) |
| `Invoke-DbIntelligenceIndex.ps1` | Index job against a path (API must already be up) |
| `Invoke-DbIntelligenceBatchIndex.ps1` | Batch-index children under a parent (`D:\code\projects` or `C:\code`) |
| `Invoke-DbIntelligenceCombine.ps1` | Present all child `graph.json` as one live graph |

```powershell
.\scripts\Install-DbIntelligencePrereqs.ps1 -Yes
.\scripts\Build-DbIntelligence.ps1 -SkipWeb
.\scripts\Test-DbIntelligenceHealth.ps1
```

CLI equivalents:

```powershell
dotnet run --project .\DbIntelligence.Cli -- --health
dotnet run --project .\DbIntelligence.Cli -- --install-preqs --yes
```

## Prerequisites

- .NET 8 SDK, Node.js 18+ / npm, Python 3.10+
- Prefer **fnm** user install (no admin): `.\scripts\Initialize-DbIntelligenceNode.ps1 -Install -Yes`
- Graphify: `python -m pip install graphifyy` → `graphify` on PATH
- Codegraph: prefer `fnm exec --using=lts-latest -- npm i -g @colbymchenry/codegraph` (PATH `npm` / official script only as fallback); verify `codegraph -V`
- Optional SQL connection string for inventory scan (user secrets / env)

Health:

- `GET /api/health` → **200** healthy / **503** unhealthy  
- `GET /api/tools` → detailed report  

## Index job pipeline

1. Verify Python / Graphify / Codegraph  
2. Codegraph init/sync on the target path  
3. `graphify extract <path> --code-only` and import `graphify-out/graph.json` (supports numeric `community` and NetworkX `links`)  
4. Roslyn repository SQL/SP scan  
5. Optional SQL inventory  
6. Merge + export maps  

Artifacts: under `{repo}/.db-index/` — `graph.json`, `code-to-db-map.json`, `stored-procedure-map.json`, `code-reference-locations.json`, `GRAPH_REPORT.md`, `findings.html`.

### Index via API (PowerShell)

```powershell
$body = @{
  targetRepositoryPath = "D:\path\to\repo"
  runCodegraph         = $true
  runGraphify          = $true
  runRepositoryScan    = $true
  runSqlScan           = $true
  sqlConnectionString  = "Server=(localdb)\mssqllocaldb;Database=ShowcaseOwned;Trusted_Connection=True;TrustServerCertificate=True"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5088/api/index/jobs" `
  -Method Post -Body $body -ContentType "application/json"
```

SP-focused helper (infers Showcase LocalDB placeholders from kit `appsettings.json` + enum tokens):

```powershell
.\scripts\Invoke-DbIntelligenceExtractSps.ps1 -UseShowcaseLocalDefaults
.\scripts\Invoke-DbIntelligenceIndex.ps1 -RepositoryPath "D:\path\to\repo" -SqlConnectionString "<non-prod cs>"
.\scripts\Export-DatabaseStoredProcedures.ps1 -OutputFile "D:\exports\all-sps.sql" -SqlConnectionString "<non-prod cs>"
```

## Solution projects

Open [`../DatabaseModernization.sln`](../DatabaseModernization.sln).

| Project | Role |
|---------|------|
| `DbIntelligence.Api` | HTTP API + optional SPA host |
| `DbIntelligence.Cli` | `--health` / `--install-preqs` |
| `DbIntelligence.Infrastructure` | CLI runners, merge, store, export |
| `DbIntelligence.RepositoryScanner` | Roslyn code→SQL/SP mapper |
| `DbIntelligence.SqlScanner` | Read-only SQL inventory |
| `DbIntelligence.Web` | Angular + vis-network |
| `DbIntelligence.Worker` | Optional background indexing |
| `DbIntelligence.Domain` / `Contracts` | Graph model + DTOs |
| `DbIntelligence.Tests` | Unit / import fixtures |

## Configure

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

```powershell
$env:DbIntelligence__TargetRepositoryPath = "D:\path\to\repo"
cd DbIntelligence.Api
# Prefer Showcase LocalDB placeholders from appsettings.json (no secrets), or set explicitly:
dotnet user-secrets set "DbIntelligence:SqlConnectionString" "Server=(localdb)\mssqllocaldb;Database=ShowcaseOwned;Trusted_Connection=True;TrustServerCertificate=True"
```

## API surface (summary)

| Endpoint | Behavior |
|----------|----------|
| `POST /api/index/jobs` | Start index |
| `GET /api/index/jobs/{id}` | Status |
| `GET /api/search?q=` | Search |
| `GET /api/explore?q=` | Neighborhood |
| `GET /api/graphs/unified` | Graph JSON |
| `GET /api/maps/code-to-db` | Code→DB map |
| `GET /api/maps/stored-procedures` | SP map |
| `POST /api/export` | Write artifacts |

## Safety

- SqlScanner is **read-only**.
- Never commit real connection strings.
- Dynamic SQL edges are `AMBIGUOUS` for human review.
