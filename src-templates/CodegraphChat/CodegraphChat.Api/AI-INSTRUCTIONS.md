# AI Instructions — `CodegraphChat.Api`

## Purpose

Minimal ASP.NET Core host for the Codegraph topic chat API (`/api/health`, `/api/session`, `/api/session/ensure-index`, `/api/chat`).

## Rules

1. Keep endpoints thin; orchestration lives in `CodegraphChat.Infrastructure`.
2. Do not invent repository paths or credentials — callers supply a local path that already has `.codegraph/`.
3. CORS origin for the Angular UI is `http://localhost:4201`.
4. Prefer PowerShell start scripts under `../scripts/`.
