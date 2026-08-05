# AI Instructions — `CodegraphChat.Web`

## Purpose

Angular 18 SPA: ChatGPT-like topic chat over Codegraph (proxies `/api` → `http://localhost:5091`).

## Rules

1. Prefer `../scripts/Start-CodegraphChatWeb.ps1` (fnm via DbIntelligence helper) for hot reload.
2. Prefer `../scripts/Build-CodegraphChat.ps1` to publish production SPA into `../CodegraphChat.Api/wwwroot`.
3. Do not hard-code production repository paths or secrets.
4. Keep visual language aligned with DbIntelligence.Web (Fraunces + IBM Plex, paper/ink palette).
