# DbIntelligence.Web

Angular 18 + vis-network SPA for the evidence graph, code→DB maps, and index job controls.

Proxies `/api` → `http://localhost:5088` in development.

## Run (PowerShell)

Preferred path after indexing (or after Ready started the API):

```powershell
cd ..
.\scripts\Invoke-DbIntelligenceReady.ps1 "D:\path\to\your\app"   # once: tools + API + index
.\scripts\Start-DbIntelligenceWeb.ps1                            # UI :4200 (fnm Node, no admin)
```

API-only + UI:

```powershell
cd ..
.\scripts\Initialize-DbIntelligenceNode.ps1 -Install -Yes   # once
.\scripts\Start-DbIntelligence.ps1 -Force                   # API :5088
.\scripts\Start-DbIntelligenceWeb.ps1                       # UI  :4200
```

Equivalent without kit scripts:

```powershell
. ..\scripts\Initialize-DbIntelligenceNode.ps1
npm install
npm start
```

Full guide: [`../README.md`](../README.md) · root [`../../../HOW-TO-USE.md`](../../../HOW-TO-USE.md).

## Notes

- Do not hard-code production repository paths or secrets in the UI.
- Prefer `Start-DbIntelligenceWeb.ps1` over inventing install steps.
- Scripts use ASCII punctuation only (Windows PowerShell 5.1 rejects Unicode em dashes in `.ps1` strings).
- Codegraph (used by the API indexer) is installed with `fnm exec --using=lts-latest -- npm i -g @colbymchenry/codegraph` when fnm is present — not via this Angular project.
