# Showcase cutover demo (owners)

Scripted blue→shadow→green demo using the golden **ShowcaseDataService** template. No production SQL is executed by these steps.

Production gates: also complete [`checklists/production-cutover.md`](../../../checklists/production-cutover.md).

## Prerequisites

- DbIntelligence index + exported `code-to-db-map.json` / `stored-procedure-map.json`
- .NET 8 SDK; optional Docker for Compose blue/green
- Connection strings for Source (monolith) and Owned (target) — **never invent credentials**

## Script

### 1. Index & export findings

```powershell
cd src-templates\DbIntelligence
.\scripts\Invoke-DbIntelligenceReady.ps1 "D:\path\to\your\app"
# Export / locate artifacts under {repo}/.db-index/
```

### 2. Package domain

```powershell
cd src-templates\FindingsMigration
.\scripts\Invoke-FindingsMigration.ps1 `
  -CodeToDbMap "...\code-to-db-map.json" `
  -StoredProcedureMap "...\stored-procedure-map.json" `
  -DomainName "Insight"
```

Review `out\Insight\FINDINGS-REVIEW.md` — AMBIGUOUS is not owned.

### 3. Scaffold from Showcase golden

```powershell
.\scripts\New-DomainFromFindings.ps1 `
  -DomainName "Insight" `
  -PackageDirectory ".\out\Insight" `
  -StoredProcedureMap "...\stored-procedure-map.json" `
  -CopyManifestsToKit
```

### 4. Blue (SourceFacade)

```powershell
cd ..\DataServices\ShowcaseDataService\ShowcaseDataService.Api
$env:MigrationRouting__Slot = "Blue"
$env:MigrationRouting__DefaultRoute = "SourceFacade"
dotnet run --launch-profile blue
# http://localhost:5081/  dashboard · /swagger · header X-Data-Access-Route: SourceFacade
```

Or Compose:

```powershell
cd ..\deploy
docker compose --profile blue up --build
```

### 5. Shadow compare (evidence)

Send reads with `X-Data-Access-Route: Shadow`. Open `/` dashboard — matching vs mismatching shadow diffs. **No dual-write.**

### 6. Green (Owned)

```powershell
$env:MigrationRouting__Slot = "Green"
$env:MigrationRouting__DefaultRoute = "Owned"
dotnet run --launch-profile green
# http://localhost:5082/
```

Compose: `docker compose --profile green up --build`

### 7. EKS weight switch (template)

```powershell
helm template showcase .\deploy\helm\showcase-dataservice `
  --set ingress.blueWeight=80 --set ingress.greenWeight=20
# Raise greenWeight only after owner + DBA approval
```

### 8. Approve

- Domain owner signs ownership manifests
- DBA reviews SQL project vs EF split + RBAC (`sql/common/21-create-rbac-roles.sql` is DBA-review only)
- Complete production cutover checklist before real traffic

## Rollback

Set route/slot back to Blue / SourceFacade (or ingress blueWeight=100). Do not drop owned objects without DBA review.
