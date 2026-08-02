# ShowcaseDataService — golden DB-as-a-Service template

Buildable, replicable data-service template for this kit. **CustomerDataService** remains a thin example; FindingsMigration scaffolds new domains from **this** template.

## Features

- Hybrid ownership: EF for owned tables (`showcase.Items`), Dapper fluent `ExecuteSp` / `ExecuteSql` with mapping + latency monitoring
- FacadeThenMove routes: `SourceFacade` | `Owned` | `Shadow` (header `X-Data-Access-Route`)
- Blue/Green slot: header `X-Blue-Green-Slot` (`Blue` | `Green`)
- Owner dashboard at `/` (shadow diffs, EF vs SP vs SQL speed)
- Benchmark: `GET /api/showcase/items/{id}/benchmark`
- BuildingBlocks: DataAccess (fluent), Observability, Security, Migration
- Deploy: Docker Compose + EKS Helm (blue + green)

## Run locally

```powershell
cd src-templates\DataServices\ShowcaseDataService\ShowcaseDataService.Api
dotnet run
# http://localhost:5080 (or launchSettings) · Swagger · /
```

```powershell
cd ..\deploy
docker compose --profile blue --profile green up --build
```

## Cutover demo

See [`SHOWCASE-CUTOVER.md`](SHOWCASE-CUTOVER.md).
