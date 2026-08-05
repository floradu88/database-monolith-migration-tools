# CodegraphChat.Web

Angular 18 chat UI. Prefer the **single-host** Ready path (SPA inside the API) unless you need hot reload.

## Preferred: one command (no separate UI process)

```powershell
cd ..\scripts
.\Invoke-CodegraphChatReady.ps1 "D:\path\to\your\app"
```

Open http://localhost:5091/ (SPA published to `../CodegraphChat.Api/wwwroot` via **fnm** npm).

## Optional: hot reload on :4201

Activates user-scoped fnm Node (DbIntelligence helper); prefers `fnm exec --using=lts-latest -- npm`:

```powershell
cd ..\scripts
.\Start-CodegraphChat.ps1 -Force -RepositoryPath "D:\path\to\your\app"   # API first
.\Start-CodegraphChatWeb.ps1 -Yes
```

Proxies `/api` → `http://localhost:5091`. Open http://localhost:4201

Do not invent alternate Node installers — use `..\DbIntelligence\scripts\Initialize-DbIntelligenceNode.ps1`.
