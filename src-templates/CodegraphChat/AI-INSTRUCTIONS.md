# AI Instructions — `src-templates/CodegraphChat`

## Purpose

Separate ChatGPT-style topic chat over an **already mapped** Codegraph index. Complements DbIntelligence (which builds evidence graphs / code→DB maps) with a conversational query UI.

## Mandatory workflow

1. Read this folder's `README.md` and root `HOW-TO-USE.md`.
2. Prefer `scripts/*.ps1`; reuse DbIntelligence fnm/Codegraph install helpers — do not invent alternate Node install paths.
3. Do not invent credentials, production repository paths, or LLM API keys. Answers come from Codegraph CLI evidence.
4. Keep SQL/DB concerns out of this project — it is Codegraph-only.
5. Preserve Graphify/DbIntelligence ownership boundaries; this tool only reads `.codegraph/` via CLI.

## Current subfolders

- `CodegraphChat.Api/`
- `CodegraphChat.Contracts/`
- `CodegraphChat.Infrastructure/`
- `CodegraphChat.Web/`
- `CodegraphChat.Tests/`
- `scripts/`

## Completion report

Report changed files, assumptions, validation, unresolved risks, and required approval.
