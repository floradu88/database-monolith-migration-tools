# AI Instructions — `src-templates/CodegraphChat/scripts`

## Purpose

PowerShell operator entrypoints for CodegraphChat.

## Rules

1. Prefer `Invoke-CodegraphChatReady.ps1` with **path only** for local run (auto-confirms installs like DbIntelligence Ready).
2. Reuse `../DbIntelligence/scripts/Initialize-DbIntelligenceNode.ps1` for Node/fnm/Codegraph — do not invent alternate installers.
3. Prefer Codegraph and npm via `fnm exec --using=lts-latest` when fnm is present.
4. `Build-CodegraphChat.ps1` publishes Angular production output into `../CodegraphChat.Api/wwwroot`.
5. ASCII punctuation only in `.ps1` strings (Windows PowerShell 5.1).
6. Never invent production paths or credentials.

## Scripts

- `Invoke-CodegraphChatReady.ps1` — one command (fnm + build + start)
- `Setup-CodegraphChat.ps1` — prereqs + build
- `Build-CodegraphChat.ps1` — restore/build/test + SPA → wwwroot (fnm npm)
- `Start-CodegraphChat.ps1` — API :5091
- `Start-CodegraphChatWeb.ps1` — Angular :4201 (fnm npm)
