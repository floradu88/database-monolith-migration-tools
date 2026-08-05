# AI Instructions — `src-templates/CodegraphChat/scripts`

## Purpose

PowerShell operator entrypoints for CodegraphChat (setup, build/publish SPA, API + Angular UI).

## Rules

1. Reuse `../DbIntelligence/scripts/Initialize-DbIntelligenceNode.ps1` for Node/fnm/Codegraph — do not invent alternate installers.
2. Prefer Codegraph via `fnm exec --using=lts-latest -- npm i -g @colbymchenry/codegraph` when fnm is present.
3. `Build-CodegraphChat.ps1` publishes Angular production output into `../CodegraphChat.Api/wwwroot`.
4. ASCII punctuation only in `.ps1` strings (Windows PowerShell 5.1).
5. Never invent production paths or credentials; callers pass a local mapped repo path.

## Scripts

- `Setup-CodegraphChat.ps1` — prereqs + build
- `Build-CodegraphChat.ps1` — restore/build/test + SPA → wwwroot
- `Invoke-CodegraphChatReady.ps1` — build + start API
- `Start-CodegraphChat.ps1` — API :5091
- `Start-CodegraphChatWeb.ps1` — Angular :4201
