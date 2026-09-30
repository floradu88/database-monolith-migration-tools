# SQL script poller

Hangfire worker that polls an inbox of `.sql` files and applies them with DbUp to SQL Server or PostgreSQL, including cloud databases via the connection string.

Kit how-to: [`../../HOW-TO-USE.md`](../../HOW-TO-USE.md).

## Requirements

- .NET 9 SDK (`dotnet --version` should report a 9.x SDK; `global.json` pins `9.0.314`)

## How to run

From the repository root:

```powershell
cd C:\code\projects\database-monolith-migration-tools

dotnet run --project src/MigrationTool.Host -- --poll-once
```

That runs one poll and exits. Leave off `--poll-once` to keep the process up. The Hangfire dashboard is [http://127.0.0.1:5088/hangfire](http://127.0.0.1:5088/hangfire) and only accepts localhost. Job storage is in memory; the schedule is registered again on startup.

DbIntelligence’s API also defaults to port **5088**. If both are running, set `Urls` in [`appsettings.json`](appsettings.json) to another localhost port.

## Configuration

Set the target database in [`appsettings.json`](appsettings.json) or with `MIGRATION_CONNECTION_STRING`. Placeholder cloud strings are in [`../../appsettings.example.json`](../../appsettings.example.json). Do not commit passwords.

| Setting | Meaning |
|---------|---------|
| `Provider` | `SqlServer` or `PostgreSql` |
| `InboxPath` / `SuccessPath` / `FailedPath` | Script folders. Defaults are `scripts/inbox`, `scripts/success`, `scripts/failed` under this project |
| `PollCron` | Recurring poll. Default `*/1 * * * *` |
| `CommandTimeoutSeconds` | Batch, query, diagnostic, and ALTER timeout. Default `600` |
| `MaxRetries` | Failed copies `001`–`004`, then the inbox file is removed |
| `JournalSchema` / `JournalTable` | DbUp journal on the target database |

## Scripts

Put files in `scripts/inbox/ddl`, `dml`, `data`, or `query`. A first line `-- kind: ddl|dml|data|query` works when the file is not in one of those folders.

- DDL, DML, and data run once, are journaled, and move to `scripts/success`.
- Query scripts stay in the inbox and run every poll. Each run writes `scripts/success/{name}_{utc}.result.txt`.
- A failure copies `001_{file}.sql` and `001_{file}.error.txt` into `scripts/failed`.
- `GO` lines are removed from the file. Batches still run separately.
- A missing column runs a diagnostic query, adds the column, and retries that attempt once.
