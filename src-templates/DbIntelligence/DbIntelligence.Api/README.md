# DbIntelligence.Api

ASP.NET Core 8 host for DbIntelligence HTTP APIs (`:5088`) and optional SPA hosting from `wwwroot`.

## Run (PowerShell)

```powershell
cd ..
.\scripts\Start-DbIntelligence.ps1 -Force
# health
Invoke-RestMethod http://localhost:5088/api/health
```

Prerequisites (Python / Graphify / Codegraph / user-scoped Node via fnm):

```powershell
.\scripts\Install-DbIntelligencePrereqs.ps1 -Yes
```

Codegraph prefers **fnm** when present: `fnm exec --using=lts-latest -- npm i -g @colbymchenry/codegraph`.

See [`../README.md`](../README.md) and root [`../../../HOW-TO-USE.md`](../../../HOW-TO-USE.md).
