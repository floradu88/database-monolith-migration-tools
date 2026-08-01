# DbIntelligence.Web

Angular 18 + vis-network SPA for the evidence graph, code→DB maps, and index job controls.

Proxies `/api` → `http://localhost:5088` in development.

## Run (PowerShell)

Prefer kit scripts (activates/installs **fnm** Node if needed; no admin):

```powershell
cd ..
.\scripts\Initialize-DbIntelligenceNode.ps1 -Install -Yes   # once
.\scripts\Start-DbIntelligence.ps1 -Force                   # API :5088
.\scripts\Start-DbIntelligenceWeb.ps1                       # UI  :4200
```

Equivalent:

```powershell
. ..\scripts\Initialize-DbIntelligenceNode.ps1
npm install
npm start
```

Full guide: [`../README.md`](../README.md) · root [`../../../HOW-TO-USE.md`](../../../HOW-TO-USE.md).

## Notes

- Do not hard-code production repository paths or secrets in the UI.
- Prefer `Start-DbIntelligenceWeb.ps1` over inventing install steps.
- Codegraph (used by the API indexer) is installed with `fnm exec --using=lts-latest -- npm i -g @colbymchenry/codegraph` when fnm is present — not via this Angular project.
