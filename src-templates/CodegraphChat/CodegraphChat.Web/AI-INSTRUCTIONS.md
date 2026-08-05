# AI Instructions — `CodegraphChat.Web`

## Purpose

Angular 18 SPA: ChatGPT-like topic chat over Codegraph (proxies `/api` → `http://localhost:5091`).

## Rules

1. Prefer `../scripts/Invoke-CodegraphChatReady.ps1` (path only) for single-host UI, or `../scripts/Start-CodegraphChatWeb.ps1 -Yes` for hot reload (fnm via DbIntelligence helper).
2. Prefer `../scripts/Build-CodegraphChat.ps1` to publish production SPA into `../CodegraphChat.Api/wwwroot` using `fnm exec --using=lts-latest -- npm` when fnm is present.
3. Do not hard-code production repository paths or secrets.
4. Keep visual language aligned with DbIntelligence.Web (Fraunces + IBM Plex, paper/ink palette).
