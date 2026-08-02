# BuildingBlocks — minimal shared packages for ShowcaseDataService

| Package | Role |
|---------|------|
| DataAccess.Abstractions | Fluent `IDataAccessContext` / `IFluentQuery<T>`, timing + compare contracts |
| DataAccess.Dapper | `ExecuteSp` / `ExecuteSP` / `ExecuteSql` + `AddFluentDataAccess` |
| DataAccess.EfCore | `ExecuteEf` / `ExecuteQuery` fluent + migration history schema |
| Migration | Route/slot/method options + shadow compare |
| Observability | OpenTelemetry ASP.NET / Http / SqlClient |
| Security | Least-privilege connection options; reject `db_owner` runtime |

## Fluent access (all three)

```csharp
// Stored procedure (sync or async)
var rows = context.ExecuteSP<MyDto>("showcase.GetShowcaseSummary")
    .On("Owned")
    .WithParameters(new { Id = id })
    .Named("GetShowcaseSummary")
    .Timeout(30)
    .Map(r => r with { Source = "SP" })
    .ToList();

await context.ExecuteSql<MyDto>("SELECT Id, Name FROM showcase.Items WHERE Id = @Id")
    .WithParameters(new { Id = id })
    .Named("GetShowcaseSummary")
    .FirstOrDefaultAsync();

// EF Core (same timing store)
await db.ExecuteEf(d => d.Set<Item>().AsNoTracking().Where(x => x.Id == id), timings)
    .Named("GetShowcaseSummary")
    .Map(x => new MyDto(...))
    .ToListAsync();

// Compare which is faster
var cmp = await context.CompareAsync("GetShowcaseSummary", "Owned", ef, sp, sql);
// cmp.Fastest == EfCore | StoredProcedure | PlainSql
```

Factory extensions: `factory.ExecuteSP<T>(...)`, `factory.ExecuteSql<T>(...)`.
