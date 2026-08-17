# ShowcaseDataService — golden DB-as-a-Service template

Buildable, replicable data-service template for this kit. **CustomerDataService** remains a thin example; FindingsMigration scaffolds new domains from **this** template.

## Features

- Hybrid ownership (see `ShowcaseDataService.Database/object-ownership.yml`):
  - **SQL project** owns SPs, schemas, security, `deployment.DatabaseContract` (desired-state → dacpac)
  - **EF migrations** own selected tables only (`showcase.Items`)
  - **Cutover/** holds ordered up/down waves (not SSDT Build)
- **Single .NET config place** — `Database` in `appsettings.json` (`Schema`, `Owned` / `SourceFacade` endpoints). Host providers: **OnPrem | Azure | Aws** (see [`DATABASE-HOSTING.md`](DATABASE-HOSTING.md)). Env: `Database__Owned__Provider`, `Database__Owned__ConnectionString`, …
- **Templated SP names** — use enums/constants for `$"{ValueA}_{ValueB}"` holes (`ShowcaseProcedureNames` + `StoredProcedureName`). DbIntelligence expands them for discovery; FindingsMigration emits per-combination SQL stubs.
- Fluent `ExecuteSP` / `ExecuteSql` / `ExecuteEf` with mapping + latency compare
- Headers: `X-Data-Access-Route` (SourceFacade|Owned|Shadow|ParallelWrite), `X-Blue-Green-Slot`, `X-Data-Access-Method` (EfCore|StoredProcedure|PlainSql)
- Owner dashboard at `/` (shadow diffs, dbo→core parallel writes, table integrity, DAL speed, SLO p95/error counters)
- Work items: `POST /api/showcase/work-items`, `DELETE /api/showcase/work-items/{id}`, `GET /api/showcase/work-items/integrity`
- Benchmark: `GET /api/showcase/items/{id}/benchmark`
- JWT/MI-ready auth placeholder (`Auth:RequireJwt` lab-off by default — [`AUTH.md`](AUTH.md))
- Pre/PostDeploy human-gated — [`ShowcaseDataService.Database/Scripts/README.md`](ShowcaseDataService.Database/Scripts/README.md)
- Deploy: Docker Compose + EKS Helm (blue + green)

## Database layout

```text
ShowcaseDataService.Database/
├── Programmability/          # SP desired state (Build)
├── Contract/                 # version / health surface (Build)
├── Security/                 # schemas (Build)
├── Scripts/Pre|PostDeploy    # foundation + contract stamp
├── Cutover/                  # NNN_*.up.sql / *.down.sql (None)
├── Reference/EfOwned/        # EF table shapes — not Build
└── object-ownership.yml
ShowcaseDataService.Migrations/  # EF-owned tables only
```

Deploy order: PreDeploy → EF migrations → dacpac → Cutover ups → PostDeploy. Details: [`ShowcaseDataService.Database/README.md`](ShowcaseDataService.Database/README.md).

## Run locally

```powershell
# One-time LocalDB lab (Owned + SourceFacade DBs, EF Items, Showcase SPs)
cd src-templates\DataServices\ShowcaseDataService
.\scripts\Initialize-ShowcaseLocalDb.ps1

cd ShowcaseDataService.Api
dotnet run
```

Export SPs after init:

```powershell
cd src-templates\DbIntelligence
.\scripts\Export-DatabaseStoredProcedures.ps1 `
  -OutputFile "$env:TEMP\ShowcaseOwned-procedures.sql" `
  -UseShowcaseLocalDefaults
```
# http://localhost:5080 (or launchSettings) · Swagger · /
```

```powershell
cd ..\deploy
docker compose --profile blue --profile green up --build
```

## Cutover demo

See [`SHOWCASE-CUTOVER.md`](SHOWCASE-CUTOVER.md). Hosting (OnPrem / Azure / Aws pros & cons): [`DATABASE-HOSTING.md`](DATABASE-HOSTING.md). Kit projects overview: [`../../../docs/PROJECT-GUIDE.md`](../../../docs/PROJECT-GUIDE.md).
