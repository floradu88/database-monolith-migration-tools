# AI Instructions — `src-templates/CodegraphChat`

## Purpose

Separate ChatGPT-style topic chat over a Codegraph index. Complements DbIntelligence (evidence graphs / code→DB maps) with a conversational query UI. Prefer single-host (API serves SPA from `wwwroot`) or dual-host Angular `:4201`.

## Mandatory workflow

1. Read this folder's `README.md` and root `HOW-TO-USE.md`.
2. Prefer `scripts/*.ps1`; reuse DbIntelligence fnm/Codegraph install helpers — do not invent alternate Node install paths.
3. Prefer Codegraph via `fnm exec --using=lts-latest -- npm i -g @colbymchenry/codegraph` when fnm is present.
4. Do not invent credentials, production repository paths, or LLM API keys. Answers come from Codegraph CLI evidence.
5. Keep SQL/DB concerns out of this project — it is Codegraph-only.
6. Publish Angular with `scripts/Build-CodegraphChat.ps1` into `CodegraphChat.Api/wwwroot` (generated assets are gitignored).
7. Preserve Graphify/DbIntelligence ownership boundaries; this tool only reads/updates `.codegraph/` via CLI.

## Current subfolders

- `CodegraphChat.Api/`
- `CodegraphChat.Contracts/`
- `CodegraphChat.Infrastructure/`
- `CodegraphChat.Web/`
- `CodegraphChat.Tests/`
- `scripts/` — Setup / Build / Ready / Start (API + Web)

## Completion report

Report changed files, assumptions, validation, unresolved risks, and required approval.
