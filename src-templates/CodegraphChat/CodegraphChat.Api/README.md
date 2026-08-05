# CodegraphChat.Api

HTTP API for topic chat over a pre-indexed Codegraph project.

- Port: `http://localhost:5091`
- Endpoints: `/api/health`, `/api/session`, `/api/session/ensure-index`, `/api/chat`
- After `.\scripts\Build-CodegraphChat.ps1`, the Angular SPA is served from `wwwroot` at http://localhost:5091/

Start:

```powershell
cd ..\scripts
.\Build-CodegraphChat.ps1 -Yes
.\Start-CodegraphChat.ps1 -Force
```
