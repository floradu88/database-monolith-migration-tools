# DbIntelligence local runbook (plan snapshot)

Canonical human guide: [`../HOW-TO-USE.md`](../HOW-TO-USE.md).

Cursor implementation plan (completed): `dbintelligence_graph_stack` — all todos done including PowerShell setup and local smoke.

## PowerShell quick path

```powershell
cd src-templates\DbIntelligence

# User-scoped Node/npm + Codegraph via fnm exec (no admin) — also invoked by Setup/Prereqs/Build/Web
.\scripts\Initialize-DbIntelligenceNode.ps1 -Install -Yes

.\scripts\Setup-DbIntelligence.ps1 -Yes
.\scripts\Start-DbIntelligence.ps1 -Force
.\scripts\Start-DbIntelligenceWeb.ps1
.\scripts\Invoke-DbIntelligenceIndex.ps1 -RepositoryPath "D:\path\to\repo"

# Batch: one project per child folder (artifacts written to each project root)
.\scripts\Invoke-DbIntelligenceBatchIndex.ps1 -ParentFolderPath "D:\code\projects"
.\scripts\Invoke-DbIntelligenceBatchIndex.ps1 -ParentFolderPath "C:\code"
```

## Delivered

| Area | Location |
|------|----------|
| API + health | `DbIntelligence.Api` · `:5088` |
| Live maps | **In memory** (`FileIntelligenceStore`) — no DB yet |
| Durable export | `artifacts/db-intelligence/*.json` (or project root for batch) |
| Angular UI | `DbIntelligence.Web` · `:4200` |
| CLI health/install | `DbIntelligence.Cli` |
| Scanners | `RepositoryScanner`, `SqlScanner` |
| Scripts | `scripts/*.ps1` (fnm Node + Codegraph via `fnm exec`, setup/run/index/batch) |
| Docs | Root `HOW-TO-USE.md`, `README.md`, this runbook |

## Node without admin

Prefer **fnm** via `winget --scope user` + Node LTS in the user profile. **Codegraph** installs with `fnm exec -- npm i -g @colbymchenry/codegraph` when fnm is present. See `Initialize-DbIntelligenceNode.ps1` (`-Install` / `-InstallCodegraph`) and [`../HOW-TO-USE.md`](../HOW-TO-USE.md) § “Node.js without admin (fnm)”.

## Graphify CLI contract

- Run: `graphify extract <path> --code-only`
- Import: `graphify-out/graph.json` with NetworkX `links` and numeric `community`
