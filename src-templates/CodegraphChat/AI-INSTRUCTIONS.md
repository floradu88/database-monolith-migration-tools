# AI Instructions — `src-templates/CodegraphChat`

## Purpose

Separate ChatGPT-style topic chat over a Codegraph index. Complements DbIntelligence with a conversational query UI.

## One command

```powershell
.\scripts\Invoke-CodegraphChatReady.ps1 "D:\path\to\your\app"
```

Uses DbIntelligence `Initialize-DbIntelligenceNode.ps1` (fnm, no admin) and Codegraph via `fnm exec --using=lts-latest`. Publishes SPA to `Api/wwwroot`. Open http://localhost:5091/

## Mandatory workflow

1. Read this folder's `README.md` and root `HOW-TO-USE.md`.
2. Prefer `scripts/Invoke-CodegraphChatReady.ps1` (path only) for local run — do not invent alternate Node installers.
3. Prefer Codegraph via `fnm exec --using=lts-latest -- npm i -g @colbymchenry/codegraph` when fnm is present.
4. Do not invent credentials, production repository paths, or LLM API keys.
5. Keep SQL/DB concerns out of this project.
6. Publish Angular with Ready/`Build-CodegraphChat.ps1` into `CodegraphChat.Api/wwwroot` (gitignored except `.gitkeep`).

## Current subfolders

- `CodegraphChat.Api/` · `Contracts/` · `Infrastructure/` · `Web/` · `Tests/` · `scripts/`

## Completion report

Report changed files, assumptions, validation, unresolved risks, and required approval.
