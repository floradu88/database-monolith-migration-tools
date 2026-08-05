# ShowcaseDataService.Migrations

EF Core migrations project for the Showcase golden template.

## Owns

- `showcase.Items` (and other EF-selected tables only)

## Does not own

- Stored procedures, schemas, security, `deployment.DatabaseContract` — those live in `ShowcaseDataService.Database`.

See [`../ShowcaseDataService.Database/object-ownership.yml`](../ShowcaseDataService.Database/object-ownership.yml).

## Deploy order

Run **after** SQL PreDeploy and **before** SQL project dacpac / cutover ups when bringing up Owned.

```powershell
$env:Database__OwnedConnectionString = "<local non-prod connection — do not invent prod credentials>"
$env:Database__Schema = "showcase"   # or dbo during early façade
dotnet ef database update --project .\ShowcaseDataService.Migrations --startup-project .\ShowcaseDataService.Api
```

Lab shortcut (creates LocalDB DBs + applies PreDeploy / EF / Programmability):

```powershell
cd ..\scripts
.\Initialize-ShowcaseLocalDb.ps1
```

Never overlap object ownership with the SQL project. Never auto-apply destructive migrations to production.

Legacy aliases: `SHOWCASE_DB_CONNECTION`, `SHOWCASE_DB_SCHEMA`, and `SqlConnections__*` still work via fallback.
