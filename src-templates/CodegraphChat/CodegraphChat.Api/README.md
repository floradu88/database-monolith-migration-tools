# CodegraphChat.Api

HTTP API for topic chat (+ SPA from `wwwroot` after Ready/Build).

- Port: `http://localhost:5091`
- Endpoints: `/api/health`, `/api/session`, `/api/session/ensure-index`, `/api/chat`

## One command

```powershell
cd ..\scripts
.\Invoke-CodegraphChatReady.ps1 "D:\path\to\your\app"
```

Uses fnm Node + Codegraph (`fnm exec --using=lts-latest`), builds Angular into `wwwroot`, starts this API. Open http://localhost:5091/

API-only after a prior build:

```powershell
.\Start-CodegraphChat.ps1 -Force -RepositoryPath "D:\path\to\your\app"
```
