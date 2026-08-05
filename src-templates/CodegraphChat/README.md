# CodegraphChat

ChatGPT-style interface for asking about a **topic** in a repository indexed with Codegraph.

Answers are grounded in Codegraph CLI evidence (`query`, `callers`, `callees`, `impact`, `status`, `files`) — not inventing credentials or calling an external LLM.

Related: DbIntelligence builds/merges indexes and code→DB maps. CodegraphChat is the conversational front-end over Codegraph. **Ensure index** may run local `codegraph init` / `sync`; it does **not** replace DbIntelligence code→DB mapping.

## Layout

```text
CodegraphChat/
├── CodegraphChat.Api/             # HTTP :5091 (+ SPA from wwwroot when published)
├── CodegraphChat.Contracts/       # DTOs
├── CodegraphChat.Infrastructure/  # CLI runner + intent router + chat service
├── CodegraphChat.Web/             # Angular 18 UI :4201 (dev) / published into Api/wwwroot
├── CodegraphChat.Tests/
└── scripts/                       # PowerShell (reuses DbIntelligence fnm helper)
```

## Prerequisites

| Tool | Why |
|------|-----|
| .NET 8 SDK | API + tests |
| Node 18+ / npm (fnm preferred) | Angular UI |
| `codegraph` on PATH | Symbol index queries |

```powershell
cd ..\DbIntelligence
.\scripts\Initialize-DbIntelligenceNode.ps1 -Install -InstallCodegraph -Yes
```

## Quick start (single-host)

Builds .NET + Angular, publishes the SPA into `CodegraphChat.Api/wwwroot`, starts API:

```powershell
cd D:\code\projects\database-monolith-migration-tools\src-templates\CodegraphChat

.\scripts\Setup-CodegraphChat.ps1 -Yes          # once: tools + build + wwwroot
.\scripts\Invoke-CodegraphChatReady.ps1 "D:\path\to\your\app"
```

Open **http://localhost:5091/** (chat UI served by the API).

## Dual-host (Angular hot reload)

```powershell
.\scripts\Start-CodegraphChat.ps1 -Force -RepositoryPath "D:\path\to\your\app"   # API :5091
.\scripts\Start-CodegraphChatWeb.ps1                                             # UI  :4201
```

## Operator scripts

| Script | Purpose |
|--------|---------|
| `Setup-CodegraphChat.ps1` | Prereqs + build (no start) |
| `Build-CodegraphChat.ps1` | Restore/build/test; Angular → `Api/wwwroot` |
| `Invoke-CodegraphChatReady.ps1` | Build + start API bound to a repo path |
| `Start-CodegraphChat.ps1` | API only |
| `Start-CodegraphChatWeb.ps1` | Angular dev server |

## UI features

- Bind mapped repo path (persisted in `localStorage`)
- **Ensure index** (`codegraph init` or `sync`)
- Suggestion chips, markdown answers, Copy, symbol follow-ups (about / callers / callees / impact)
- Expandable Codegraph evidence

Example prompts: `tell me about IndexingService`, `who calls "CodegraphClient"`, `impact of EvidenceGraph`, `index status`.

## API

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/api/health` | Codegraph availability |
| GET/POST | `/api/session` | Bind mapped repository path |
| POST | `/api/session/ensure-index` | `codegraph init` or `sync` for the bound path |
| POST | `/api/chat` | Topic question → Codegraph-backed answer |

```powershell
Invoke-RestMethod -Method Post -Uri http://localhost:5091/api/session -ContentType 'application/json' -Body '{"repositoryPath":"D:\\path\\to\\your\\app"}'
Invoke-RestMethod -Method Post -Uri http://localhost:5091/api/chat -ContentType 'application/json' -Body '{"message":"who calls IndexingService"}'
```

## Notes

- Prefer quoting symbols: `who calls "TopicChatService"`.
- Mode override forces `query|callers|callees|impact|status|files`.
- Generated SPA under `Api/wwwroot` is gitignored (except `.gitkeep`); rebuild with `Build-CodegraphChat.ps1`.
