# BuildingBlocks — minimal shared packages for ShowcaseDataService

Filled only what the golden Showcase template needs:

| Package | Role |
|---------|------|
| DataAccess.Abstractions | `IDbConnectionFactory`, fluent `IDataAccessContext` / `IFluentQuery<T>`, timing store |
| DataAccess.Dapper | `ExecuteSp` / `ExecuteSql` fluent API + connection factory |
| DataAccess.EfCore | SQL Server + `deployment.__EFMigrationsHistory` convention |
| Migration | `DataAccessRoute`, `BlueGreenSlot`, shadow compare store |
| Observability | OpenTelemetry ASP.NET / Http / SqlClient |
| Security | Least-privilege connection options; reject `db_owner` runtime |

## Fluent access (SP + plain SQL)

```csharp
// Stored procedure
var rows = await context.ExecuteSp<MyDto>("showcase.GetShowcaseSummary")
    .On("Owned")
    .WithParameters(new { Id = id })
    .Named("GetShowcaseSummary")
    .Map(r => r with { Source = "SP" })
    .ToListAsync();

// Plain SQL
var one = await context.ExecuteSql<MyDto>("SELECT Id, Name FROM showcase.Items WHERE Id = @Id")
    .WithParameters(new { Id = id })
    .Named("GetShowcaseSummary")
    .FirstOrDefaultAsync();

// Same via factory extensions
await factory.ExecuteSql<MyDto>("SELECT 1 AS Id").ToListAsync();
```

Timings land in `IDataAccessTimingStore` (avg / p95) so owners can see which method is faster on the Showcase dashboard / `/api/showcase/items/{id}/benchmark`.

Do not invent credentials. Full MigrationControlPlane remains a later wave.
