# CodegraphChat

ChatGPT-style interface for asking about a **topic** in a repository you already mapped with Codegraph.

Answers are grounded in Codegraph CLI evidence (`query`, `callers`, `callees`, `impact`, `status`, `files`) — not inventing credentials or calling an external LLM.

Related: DbIntelligence builds/merges indexes and code→DB maps; this project is a thin conversational front-end over Codegraph for an already-indexed path.

## Layout

```text
CodegraphChat/
├── CodegraphChat.Api/             # HTTP :5091
├── CodegraphChat.Contracts/       # DTOs
├── CodegraphChat.Infrastructure/  # CLI runner + intent router + chat service
├── CodegraphChat.Web/             # Angular 18 UI :4201
├── CodegraphChat.Tests/
└── scripts/                       # PowerShell (reuses DbIntelligence fnm helper)
```

## Prerequisites

Same kit stack as DbIntelligence:

| Tool | Why |
|------|-----|
| .NET 8 SDK | API + tests |
| Node 18+ / npm (fnm preferred) | Angular UI |
| `codegraph` on PATH | Symbol index queries |
| Existing `.codegraph/` under the target repo | Already mapped project |

```powershell
cd ..\DbIntelligence
.\scripts\Initialize-DbIntelligenceNode.ps1 -Install -InstallCodegraph -Yes
```

## Quick start

```powershell
cd D:\code\projects\database-monolith-migration-tools\src-templates\CodegraphChat

# Terminal 1 — API bound to your mapped app
.\scripts\Invoke-CodegraphChatReady.ps1 "D:\path\to\your\app"

# Terminal 2 — UI
.\scripts\Start-CodegraphChatWeb.ps1
```

Open http://localhost:4201 — bind the same path if needed, then ask e.g.:

- `tell me about IndexingService`
- `who calls CodegraphClient`
- `impact of EvidenceGraph`
- `index status`

## Manual start

```powershell
.\scripts\Start-CodegraphChat.ps1 -Force -RepositoryPath "D:\path\to\your\app"
.\scripts\Start-CodegraphChatWeb.ps1
```

## API

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/api/health` | Codegraph availability |
| GET/POST | `/api/session` | Bind mapped repository path |
| POST | `/api/chat` | Topic question → Codegraph-backed answer |

Example:

```powershell
Invoke-RestMethod -Method Post -Uri http://localhost:5091/api/session -ContentType 'application/json' -Body '{"repositoryPath":"D:\\path\\to\\your\\app"}'
Invoke-RestMethod -Method Post -Uri http://localhost:5091/api/chat -ContentType 'application/json' -Body '{"message":"who calls IndexingService"}'
```

## Notes

- Prefer quoting symbols: `who calls "TopicChatService"`.
- Mode override in the UI forces `query|callers|callees|impact|status|files`.
- Index creation stays with DbIntelligence Ready / `codegraph init` — this UI does not replace indexing.
