# CodegraphChat

ChatGPT-style interface for asking about a **topic** in a repository indexed with Codegraph.

Answers are grounded in Codegraph CLI evidence (`query`, `callers`, `callees`, `impact`, `status`, `files`) — no invented credentials, no external LLM.

Related: DbIntelligence builds code→DB maps. CodegraphChat is the conversational front-end. **Ensure index** may run local `codegraph init` / `sync`; it does **not** replace DbIntelligence mapping.

## One command (local)

Path only — installs/checks **fnm** Node + Codegraph (`fnm exec --using=lts-latest`, no admin), builds .NET + Angular, publishes SPA to `Api/wwwroot`, starts API:

```powershell
cd D:\code\projects\database-monolith-migration-tools\src-templates\CodegraphChat

.\scripts\Invoke-CodegraphChatReady.ps1 "D:\path\to\your\app"
```

Open **http://localhost:5091/**

Same pattern as DbIntelligence Ready: user-scoped fnm via `..\DbIntelligence\scripts\Initialize-DbIntelligenceNode.ps1` (not bare system npm).

## Layout

```text
CodegraphChat/
├── CodegraphChat.Api/             # HTTP :5091 (+ SPA from wwwroot)
├── CodegraphChat.Contracts/
├── CodegraphChat.Infrastructure/
├── CodegraphChat.Web/             # Angular (published into Api/wwwroot; optional :4201)
├── CodegraphChat.Tests/
└── scripts/                       # PowerShell (reuses DbIntelligence fnm helper)
```

## Prerequisites (usually automatic)

Ready installs these when missing:

| Tool | How this kit installs it |
|------|--------------------------|
| .NET 8 SDK | Manual / existing |
| Node 18+ / npm | **fnm** user-scope (`winget --scope user`) via DbIntelligence Node helper |
| `codegraph` | Prefer `fnm exec --using=lts-latest -- npm i -g @colbymchenry/codegraph` |

## Optional: setup only (no start)

```powershell
.\scripts\Setup-CodegraphChat.ps1 -Yes
```

## Optional: Angular hot reload (second terminal)

```powershell
.\scripts\Start-CodegraphChat.ps1 -Force -RepositoryPath "D:\path\to\your\app"
.\scripts\Start-CodegraphChatWeb.ps1 -Yes    # fnm npm on :4201
```

## Operator scripts

| Script | Purpose |
|--------|---------|
| `Invoke-CodegraphChatReady.ps1` | **One command** — fnm + build + start (path only) |
| `Setup-CodegraphChat.ps1` | Prereqs + build (no start) |
| `Build-CodegraphChat.ps1` | Restore/build/test; Angular via fnm → `Api/wwwroot` |
| `Start-CodegraphChat.ps1` | API only |
| `Start-CodegraphChatWeb.ps1` | Angular dev (fnm) |

## UI features

- Bind mapped repo path (persisted in `localStorage`)
- **Ensure index** (`codegraph init` or `sync`)
- Suggestion chips, markdown answers, Copy, symbol follow-ups
- Expandable Codegraph evidence

Example prompts: `tell me about IndexingService`, `who calls "CodegraphClient"`, `impact of EvidenceGraph`, `index status`.

## API

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/api/health` | Codegraph availability |
| GET/POST | `/api/session` | Bind repository path |
| POST | `/api/session/ensure-index` | `init` / `sync` |
| POST | `/api/chat` | Topic → Codegraph-backed answer |

```powershell
Invoke-RestMethod http://localhost:5091/api/health
Invoke-RestMethod -Method Post -Uri http://localhost:5091/api/chat -ContentType 'application/json' -Body '{"message":"index status"}'
```

## Notes

- Prefer quoting symbols: `who calls "TopicChatService"`.
- Generated SPA under `Api/wwwroot` is gitignored (except `.gitkeep`); rebuild with Ready or `Build-CodegraphChat.ps1`.
- Full kit reference: root [`HOW-TO-USE.md`](../../HOW-TO-USE.md).
