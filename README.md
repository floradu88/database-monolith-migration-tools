# SQL Server Monolith Decomposition and DB-as-a-Service Kit — V5 Reviewed

This repository contains the reviewed **sql-db-modernization-kit-v5** package: a production-grade path from a large shared SQL Server database to smaller, independently owned data services and databases.

It includes:

- discovery and AI-assisted repository indexing;
- SQL procedure, function, view, trigger, and DML-access tracking;
- Query Store, SQL Audit, Extended Events, and DMV collection;
- source-monolith decomposition into manageable SQL projects;
- target database projects and optional EF Core migrations;
- EF Core versus Dapper versus stored-procedure evaluation;
- migration control plane and migration manifests;
- RBAC, deployment controls, drift detection, and auditability;
- performance baselines, SLOs, observability, read scaling, elastic pools, and sharding;
- shadow reads, canaries, cutover, rollback, and decommissioning;
- example .NET solution/project templates and SQL scripts.

## Repository layout

```text
.
├── AGENTS.md / CLAUDE.md     # Claude Code + general agent entrypoints
├── .cursor/rules/            # Cursor project rules
├── AI-INSTRUCTIONS.md        # Root agent instructions (every folder has one)
├── AI-INSTRUCTION-INDEX.md   # Index of all AI-INSTRUCTIONS.md files
├── docs/                     # Architecture, plans, runbooks
├── sql/                      # Discovery, telemetry, audit, RBAC scripts
├── manifests/                # Domain + migration-wave examples
├── src-templates/            # .NET / SQL project scaffolds
│   ├── DbIntelligence/       # Index + evidence graph UI
│   ├── CodegraphChat/        # Topic chat over an existing Codegraph index
│   └── …                     # DataServices, FindingsMigration, …
├── checklists/               # Cutover and split checklists
└── validation/               # Checksums + validation summary
```

## Start here

1. **[`HOW-TO-USE.md`](HOW-TO-USE.md)** — PowerShell setup, run, and index commands (DbIntelligence + CodegraphChat + kit overview)
2. **[`docs/PROJECT-GUIDE.md`](docs/PROJECT-GUIDE.md)** — all kit projects, why use them, pros/cons
3. **[`docs/FUTURE-FEATURES.md`](docs/FUTURE-FEATURES.md)** — findings → domain project roadmap + template
4. **[`src-templates/DataServices/ShowcaseDataService/`](src-templates/DataServices/ShowcaseDataService/)** — golden DB-as-a-Service template + [`SHOWCASE-CUTOVER.md`](src-templates/DataServices/ShowcaseDataService/SHOWCASE-CUTOVER.md) + [`DATABASE-HOSTING.md`](src-templates/DataServices/ShowcaseDataService/DATABASE-HOSTING.md) (OnPrem / Azure / Aws)
5. `docs/00-master-plan.md`
6. `docs/01-target-architecture.md`
7. `docs/02-solution-and-project-structure.md`
8. `docs/03-source-monolith-split.md`
9. `docs/04-target-database-project-strategy.md`
10. `docs/05-migration-control-plane.md`
11. `docs/06-usage-tracking-and-audit.md`
12. `docs/07-data-access-strategy.md`
13. `docs/08-performance-monitoring-and-scaling.md`
14. `docs/09-rbac-security-and-change-control.md`
15. `docs/10-execution-roadmap.md`

## PowerShell command reference (local run)

All runnable local ops below are **PowerShell**. Full detail: [`HOW-TO-USE.md`](HOW-TO-USE.md).

### 0. Prerequisites check

```powershell
dotnet --list-sdks          # .NET 8 SDK
node -v                     # Node.js 18+ (prefer fnm user install)
npm -v
fnm --version               # optional but recommended (no-admin Node)
python --version            # Python 3.10+
graphify --help             # Graphify (pip package graphifyy)
codegraph -V                # Codegraph on PATH
$PSVersionTable.PSVersion   # PowerShell 5.1+
```

Node without admin (integrated into setup scripts; Codegraph prefers fnm):

```powershell
cd src-templates\DbIntelligence
.\scripts\Initialize-DbIntelligenceNode.ps1 -Install -Yes          # fnm + Node + Codegraph via fnm exec --using=lts-latest
.\scripts\Initialize-DbIntelligenceNode.ps1 -InstallCodegraph -Yes # Codegraph only (fnm exec --using=lts-latest if present)
. .\scripts\Initialize-DbIntelligenceNode.ps1   # activate in current session
```

### 1. Ready (one command — path only)

From this repo root (example path `D:\code\projects\database-monolith-migration-tools`):

```powershell
cd D:\code\projects\database-monolith-migration-tools\src-templates\DbIntelligence

.\scripts\Invoke-DbIntelligenceReady.ps1 "D:\path\to\your\app"
```

Installs/checks tools (fnm Node + Codegraph, no admin), builds, health-checks, starts API, indexes the path.

### 1b. CodegraphChat Ready (one command — path only)

Topic chat over a Codegraph index (single-host UI). Same fnm / `fnm exec --using=lts-latest` Node+Codegraph path as DbIntelligence:

```powershell
cd D:\code\projects\database-monolith-migration-tools\src-templates\CodegraphChat

.\scripts\Invoke-CodegraphChatReady.ps1 "D:\path\to\your\app"
```

Open http://localhost:5091/ — details: [`src-templates/CodegraphChat/README.md`](src-templates/CodegraphChat/README.md) · [`HOW-TO-USE.md`](HOW-TO-USE.md).

### 2. Setup kit only (no index)

```powershell
cd D:\code\projects\database-monolith-migration-tools\src-templates\DbIntelligence

.\scripts\Setup-DbIntelligence.ps1 -Yes
# Flags: -SkipPrereqs  -SkipWeb
```

Step-by-step equivalents:

```powershell
cd src-templates\DbIntelligence
.\scripts\Install-DbIntelligencePrereqs.ps1 -Yes
.\scripts\Build-DbIntelligence.ps1                 # add -SkipWeb / -SkipTests as needed
.\scripts\Test-DbIntelligenceHealth.ps1

# CLI equivalents
dotnet run --project .\DbIntelligence.Cli -- --health
dotnet run --project .\DbIntelligence.Cli -- --install-preqs --yes
```

### 2. Start API + UI

```powershell
cd src-templates\DbIntelligence

# Terminal 1 — API http://localhost:5088 (-Force replaces an existing listener)
.\scripts\Start-DbIntelligence.ps1 -Force
# Optional: .\scripts\Start-DbIntelligence.ps1 -Force -Port 5099
# Optional: .\scripts\Start-DbIntelligence.ps1 -Force -RepositoryPath "D:\code\projects\my-monolith"

# Terminal 2 — Angular UI http://localhost:4200
.\scripts\Start-DbIntelligenceWeb.ps1
```

Without scripts:

```powershell
cd src-templates\DbIntelligence
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project .\DbIntelligence.Api -c Release --urls "http://localhost:5088"

cd DbIntelligence.Web
npm install
npm start
```

Health once the API is up:

```powershell
Invoke-RestMethod http://localhost:5088/api/health
Invoke-RestMethod http://localhost:5088/api/tools
```

### 3. Index one repository

```powershell
cd src-templates\DbIntelligence

.\scripts\Invoke-DbIntelligenceIndex.ps1 `
  -RepositoryPath "D:\code\projects\my-monolith" `
  -ApiBase "http://localhost:5088"
```

Raw API:

```powershell
$body = @{
  targetRepositoryPath = "D:\code\projects\my-monolith"
  runCodegraph         = $true
  runGraphify          = $true
  runRepositoryScan    = $true
  runSqlScan           = $false
} | ConvertTo-Json

$job = Invoke-RestMethod -Uri "http://localhost:5088/api/index/jobs" `
  -Method Post -Body $body -ContentType "application/json"

do {
  Start-Sleep -Seconds 2
  $status = Invoke-RestMethod "http://localhost:5088/api/index/jobs/$($job.id)"
  Write-Host "[$($status.status)] $($status.phase) $($status.message)"
} while ($status.status -in @("Pending", "Running"))

Invoke-RestMethod "http://localhost:5088/api/maps/code-to-db"
Invoke-RestMethod "http://localhost:5088/api/maps/stored-procedures"
```

Typical artifacts (under `{repo}/.db-index/`): `graph.json`, `code-to-db-map.json`, `stored-procedure-map.json`, `GRAPH_REPORT.md`, `findings.html`.

### 4. Batch-index a parent folder (`D:\code\projects` or `C:\code`)

Each **immediate child folder** is treated as one project; indexed sequentially; durable artifacts written to **that project's root**. The parent folder gets `db-intelligence-batch-summary.json`.

Works the same for any parent that contains project folders — including **`D:\code\projects`** and **`C:\code`**.

```powershell
cd src-templates\DbIntelligence
.\scripts\Start-DbIntelligence.ps1 -Force   # if API not already running

# Option A — projects under D:\code\projects
.\scripts\Invoke-DbIntelligenceBatchIndex.ps1 -ParentFolderPath "D:\code\projects"

# Option B — projects under C:\code (create the folder first if needed)
# New-Item -ItemType Directory -Force -Path "C:\code" | Out-Null
.\scripts\Invoke-DbIntelligenceBatchIndex.ps1 -ParentFolderPath "C:\code"

# Optional: only folders that look like code projects (.git / *.sln / *.csproj / package.json / …)
.\scripts\Invoke-DbIntelligenceBatchIndex.ps1 `
  -ParentFolderPath "D:\code\projects" `
  -RequireProjectMarkers

.\scripts\Invoke-DbIntelligenceBatchIndex.ps1 `
  -ParentFolderPath "C:\code" `
  -RequireProjectMarkers
```

Discover / batch via API:

```powershell
# Discover
Invoke-RestMethod "http://localhost:5088/api/index/discover?parentFolderPath=D:\code\projects"
Invoke-RestMethod "http://localhost:5088/api/index/discover?parentFolderPath=C:\code"

# Start batch (D:\code\projects)
$body = @{ parentFolderPath = "D:\code\projects"; artifactsRelativeDirectory = "" } | ConvertTo-Json
Invoke-RestMethod "http://localhost:5088/api/index/batch" -Method Post -Body $body -ContentType "application/json"

# Start batch (C:\code)
$body = @{ parentFolderPath = "C:\code"; artifactsRelativeDirectory = "" } | ConvertTo-Json
Invoke-RestMethod "http://localhost:5088/api/index/batch" -Method Post -Body $body -ContentType "application/json"
```

Live API graph stays **in memory** (last completed project). Durable results are on disk per project.

### 5. Promote JSON findings to a domain project

```powershell
cd src-templates\FindingsMigration

.\scripts\Invoke-FindingsMigration.ps1 `
  -CodeToDbMap "D:\code\projects\my-monolith\.db-index\code-to-db-map.json" `
  -StoredProcedureMap "D:\code\projects\my-monolith\.db-index\stored-procedure-map.json" `
  -DomainName "Billing" `
  -OwnerTeam "TBD"

.\scripts\New-DomainFromFindings.ps1 `
  -DomainName "Billing" `
  -PackageDirectory ".\out\Billing" `
  -CopyManifestsToKit
```

If the indexed project lived under `C:\code` instead:

```powershell
.\scripts\Invoke-FindingsMigration.ps1 `
  -CodeToDbMap "C:\code\my-monolith\.db-index\code-to-db-map.json" `
  -StoredProcedureMap "C:\code\my-monolith\.db-index\stored-procedure-map.json" `
  -DomainName "Billing" `
  -OwnerTeam "TBD"
```

CLI equivalent:

```powershell
dotnet run --project .\FindingsMigration.Cli -c Release -- `
  --code-to-db "D:\path\to\code-to-db-map.json" `
  --sp-map "D:\path\to\stored-procedure-map.json" `
  --domain Billing `
  --out .\out\Billing `
  --owner "TBD"
```

Review `FINDINGS-REVIEW.md` before ownership approval. See [`docs/FUTURE-FEATURES.md`](docs/FUTURE-FEATURES.md) and [`src-templates/FindingsMigration/README.md`](src-templates/FindingsMigration/README.md).

### 6. Restore / build / test the solution

```powershell
cd D:\code\projects\database-monolith-migration-tools

dotnet restore src-templates\DatabaseModernization.sln
dotnet build src-templates\DatabaseModernization.sln -c Release
dotnet test src-templates\DbIntelligence\DbIntelligence.Tests\DbIntelligence.Tests.csproj -c Release
dotnet test src-templates\FindingsMigration\FindingsMigration.Tests\FindingsMigration.Tests.csproj -c Release

start src-templates\DatabaseModernization.sln
# or: dotnet sln src-templates\DatabaseModernization.sln list
```

### Script catalog

| Script | Purpose |
|--------|---------|
| `Invoke-DbIntelligenceReady.ps1` | **One command:** path only → prereqs → build → health → API → index |
| `Setup-DbIntelligence.ps1` | Prereqs → build → test → health (no index) |
| `Initialize-DbIntelligenceNode.ps1` | User-scoped Node/npm via fnm; Codegraph via `fnm exec --using=lts-latest` when present |
| `Install-DbIntelligencePrereqs.ps1` | Node/fnm + Codegraph (`fnm exec --using=lts-latest`) + Python / pip / graphifyy / codegraph |
| `Build-DbIntelligence.ps1` | `dotnet restore/build/test` (+ optional Angular) |
| `Test-DbIntelligenceHealth.ps1` | CLI `--health` |
| `Start-DbIntelligence.ps1` | API on `:5088` (`-Force` replaces listener) |
| `Start-DbIntelligenceWeb.ps1` | Angular on `:4200` |
| `Invoke-DbIntelligenceIndex.ps1` | Index one repo path (API already up) |
| `Invoke-DbIntelligenceBatchIndex.ps1` | Index every child under a parent (`D:\code\projects` or `C:\code`) |
| `Invoke-FindingsMigration.ps1` | Package JSON maps → draft domain package |
| `New-DomainFromFindings.ps1` | Scaffold DataService from Customer template |

Details: [`HOW-TO-USE.md`](HOW-TO-USE.md), [`src-templates/DbIntelligence/README.md`](src-templates/DbIntelligence/README.md).

## Cursor and Claude support

This kit is wired for AI-assisted work in **Cursor** and **Claude Code**:

| Tool | What to use |
|------|-------------|
| **Cursor** | Project rules in `.cursor/rules/` (always-on kit core + scoped rules for `docs/`, `sql/`, `manifests/`, `src-templates/`, `checklists/`, `validation/`) |
| **Claude Code** | Root `CLAUDE.md` |
| **Any agent** | `AGENTS.md`, plus the nearest folder `AI-INSTRUCTIONS.md` |

Every folder contains an `AI-INSTRUCTIONS.md` describing purpose, safety rules, how to modify that folder, and current contents. See `AI-INSTRUCTION-INDEX.md` for the full list.

Agents should:

1. read the root README, `REVIEW-REPORT.md`, and nearest `AI-INSTRUCTIONS.md`;
2. prefer additive, reversible changes and preserve ownership boundaries;
3. never invent credentials or production values;
4. never execute destructive SQL automatically;
5. finish with a completion report (files, assumptions, validation, risks, approvals).

## Important scope

The first tracking rollout records:

- DML statement execution;
- access to stored procedures, functions, views, and triggers;
- caller identity and application attribution;
- query performance and plan history;
- database object, permission, and deployment changes.

It does not initially capture old and new row values.

## Architecture principle

Decompose in this order:

```text
Discover
→ assign ownership
→ split source definitions into domain projects
→ introduce dedicated identities and namespaces
→ place a service contract in front
→ migrate schema and data
→ shadow and reconcile
→ cut over
→ revoke legacy access
→ physically scale or shard when justified
```

## Review status

This V5 package has been reviewed for structural consistency. See:

- `REVIEW-REPORT.md`
- `docs/CANONICAL-DOCUMENT-INDEX.md`
- `validation/validation-summary.json`

The numbered documents listed in the root “Start here” section are canonical. Earlier-generation documents are retained as supplemental references and identified in the canonical index.

SQL scripts are ready for DBA review, not blind production execution. Production deployment remains blocked until platform ADR, permissions, retention, and environment values are approved.
