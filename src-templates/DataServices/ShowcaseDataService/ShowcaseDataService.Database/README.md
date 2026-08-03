# ShowcaseDataService.Database

Golden **SQL project** for hybrid DB-as-a-Service ownership. Desired-state definitions publish via dacpac; cutover waves use ordered up/down scripts beside (not inside) the Build model.

## Owns (SqlProject)

| Area | Path |
|------|------|
| Schemas | `Security/Schemas.sql` |
| Contract / version surface | `Contract/DatabaseContract.sql` |
| Stored procedures | `Programmability/` (+ `Generated/` from FindingsMigration) |
| Pre / Post deploy | `Scripts/` |

## Does not own

| Object | Owner | Notes |
|--------|-------|-------|
| `showcase.Items` | `ShowcaseDataService.Migrations` (EF) | Shape documented under `Reference/EfOwned/` only |
| Cutover waves | DBA / control plane | `Cutover/*.up.sql` / `*.down.sql` are `None` (not Build) |

See [`object-ownership.yml`](object-ownership.yml).

## Versioning

- **Definitions:** git history of Build scripts + published dacpac artifact / `DatabaseContractVersion`.
- **Contract stamp:** PostDeploy merges rows into `deployment.DatabaseContract` (`schema_version`, `contract_version`, project names).
- **Cutover:** lexical `NNN_*.up.sql` / matching `*.down.sql` — reversible operations around FacadeThenMove, not the SP source of truth.

## Deploy order

1. PreDeploy  
2. EF migrations (EF-owned tables)  
3. SQL project dacpac (SPs + contract)  
4. Approved `Cutover/*.up.sql`  
5. PostDeploy (RBAC stubs + contract stamp)

Rollback: reverse cutover downs → previous dacpac → route/slot back to SourceFacade / Blue.

## Publish (local / reviewed)

```powershell
# Requires SSDT / sqlpackage. Do not point at production without DBA approval.
# SqlPackage /Action:Publish /SourceFile:bin\Debug\ShowcaseDataService.Database.dacpac /TargetConnectionString:"..."
```

Never invent credentials. Never auto-run destructive SQL.

## Related

- Parent: [`../README.md`](../README.md)
- Cutover demo: [`../SHOWCASE-CUTOVER.md`](../SHOWCASE-CUTOVER.md)
- Strategy: [`../../../../docs/04-target-database-project-strategy.md`](../../../../docs/04-target-database-project-strategy.md)
