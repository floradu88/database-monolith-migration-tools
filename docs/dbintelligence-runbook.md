# DbIntelligence local runbook (plan snapshot)

Canonical human guide: [`../HOW-TO-USE.md`](../HOW-TO-USE.md).

Cursor implementation plan (completed): `dbintelligence_graph_stack` — all todos done including PowerShell setup and local smoke.

## PowerShell quick path

```powershell
cd src-templates\DbIntelligence
.\scripts\Setup-DbIntelligence.ps1 -Yes
.\scripts\Start-DbIntelligence.ps1 -Force
.\scripts\Start-DbIntelligenceWeb.ps1
.\scripts\Invoke-DbIntelligenceIndex.ps1 -RepositoryPath "D:\path\to\repo"
```

## Delivered

| Area | Location |
|------|----------|
| API + health | `DbIntelligence.Api` · `:5088` |
| Live maps | **In memory** (`FileIntelligenceStore`) — no DB yet |
| Durable export | `artifacts/db-intelligence/*.json` |
| Angular UI | `DbIntelligence.Web` · `:4200` |
| CLI health/install | `DbIntelligence.Cli` |
| Scanners | `RepositoryScanner`, `SqlScanner` |
| Scripts | `scripts/*.ps1` |
| Docs | Root `HOW-TO-USE.md`, this folder `README.md` |

## Graphify CLI contract

- Run: `graphify extract <path> --code-only`
- Import: `graphify-out/graph.json` with NetworkX `links` and numeric `community`
