# ShowcaseDataService — golden DB-as-a-Service template

Buildable, replicable data-service template for this kit. **CustomerDataService** remains a thin example; FindingsMigration scaffolds new domains from **this** template.

## Features

- Hybrid ownership: EF for owned tables (`showcase.Items`), fluent `ExecuteSP` / `ExecuteSql` / `ExecuteEf` with mapping + latency compare
- Headers: `X-Data-Access-Route`, `X-Blue-Green-Slot`, `X-Data-Access-Method` (EfCore|StoredProcedure|PlainSql)
- Owner dashboard at `/` (shadow diffs, DAL speed, SLO p95/error counters)
- Benchmark: `GET /api/showcase/items/{id}/benchmark`
- JWT/MI-ready auth placeholder (`Auth:RequireJwt`)
- SQL Pre/PostDeploy stubs + ownership attributes
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
