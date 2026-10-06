# SQL script poller

Hangfire worker that polls an inbox of `.sql` files and applies them with DbUp to SQL Server or PostgreSQL, including cloud databases via the connection string.

Kit how-to: [`../../HOW-TO-USE.md`](../../HOW-TO-USE.md).

## Choose the right path

| Goal | Use this |
|------|----------|
| Clone the tool, set a connection string, drop `.sql` from a SQL Server project, run them on a database | **This poller** (`--poll-once`) |
| Prefer idempotent procedure scripts | Use `CREATE OR ALTER PROCEDURE` (and other `CREATE OR ALTER …`) in inbox scripts — **supported here** |
| Build / extract / publish a `.dacpac` from a `.sqlproj` | [DACPAC tools](#dacpac-from-sql-projects) — SSDT Build objects must stay declarative `CREATE …` (no `CREATE OR ALTER` / `GO` in Build files) |

Most “run my SP scripts on the DB” work should use the poller, not dacpac publish.

PowerShell / Windows console: success lines are **green**, warnings **yellow**, failures **red**. Each inbox script logs `Running` (cyan), then succeeded (green) or failed (red).

## Requirements

- .NET 10 SDK, `10.0.203` or newer on the 10.0 line, including `10.0.401` (`global.json` uses `rollForward: latestFeature`; projects target `net10.0`)

## Quick start: clone → connect → drop scripts → run

From a machine with the .NET 10 SDK:

```powershell
git clone https://github.com/floradu88/database-monolith-migration-tools.git
cd database-monolith-migration-tools

# Option A — environment (preferred; do not commit secrets)
# AWS RDS SQL Server (port 1433 after a comma on Data Source / Server):
$env:MIGRATION_CONNECTION_STRING = "Data Source=mydb.abc123.us-east-1.rds.amazonaws.com,1433;Initial Catalog=YourDb;User ID=admin;Password=***;Encrypt=True;TrustServerCertificate=False;"
# Or: Server=mydb.abc123.us-east-1.rds.amazonaws.com,1433;Database=YourDb;User Id=admin;Password=***;Encrypt=True;

# Option B — edit src/MigrationTool.Host/appsettings.json → Migration:ConnectionString
# More examples: ../../appsettings.example.json

# Validate shape + open a live connection (prints redacted string only)
dotnet run --project src/MigrationTool.Host -- --validate-connection

# DNS resolve + ICMP ping (optional) + TCP port probe (no SQL login)
dotnet run --project src/MigrationTool.Host -- --check-network
# alias: --ping

# Copy scripts exported from your SQL Server / SSDT project into the inbox.
# Prefer CREATE OR ALTER for procedures/views/functions so re-runs are safe.
# Example:
#   copy MyProc.sql src\MigrationTool.Host\scripts\inbox\ddl\
# Or add a first line: -- kind: ddl

dotnet run --project src/MigrationTool.Host -- --poll-once
```

- Succeeded DDL/DML/data scripts move to `src/MigrationTool.Host/scripts/success`.
- Failures appear under `scripts/failed` as `001_name.sql` + `001_name.error.txt` (up to `004_`).
- Leave the process running (omit `--poll-once`) to keep polling; dashboard: [http://127.0.0.1:5088/hangfire](http://127.0.0.1:5088/hangfire) (localhost only).

DbIntelligence’s API also defaults to port **5088**. If both are running, set `Urls` in [`appsettings.json`](appsettings.json) to another localhost port.

## Configuration

Set the target database in [`appsettings.json`](appsettings.json) or with `MIGRATION_CONNECTION_STRING`. Placeholder cloud strings (including **AWS RDS SQL Server on port 1433** with `Data Source=` / `Server=`) are in [`../../appsettings.example.json`](../../appsettings.example.json). Do not commit passwords.

Validate before polling:

```powershell
# DNS + ICMP (optional) + TCP port — no credentials used for login
dotnet run --project src/MigrationTool.Host -- --check-network
dotnet run --project src/MigrationTool.Host -- --ping

# Shape + network + live SQL open (SELECT @@VERSION / version())
dotnet run --project src/MigrationTool.Host -- --validate-connection
dotnet run --project src/MigrationTool.Host -- --validate-connection --connection "Data Source=host,1433;Initial Catalog=db;User ID=u;Password=p;Encrypt=True;"
```

`--check-network` / `--ping` resolve the host, attempt ICMP ping (often blocked on AWS), then open a TCP socket to the DB port (1433 / 5432). TCP success is required; ICMP is informational. `--validate-connection` runs the same network steps, then opens the database. Passwords are redacted in logs.

Connection failures are classified with guidance (login failed, database missing, network/timeout, firewall, TLS/Encrypt). The poller probes the connection (including network) before running inbox scripts and skips the poll with a clear error if the database is unreachable.

| Setting | Meaning |
|---------|---------|
| `Provider` | `SqlServer` or `PostgreSql` |
| `InboxPath` / `SuccessPath` / `FailedPath` | Script folders. Defaults are `scripts/inbox`, `scripts/success`, `scripts/failed` under this project |
| `PollCron` | Recurring poll. Default `*/1 * * * *` |
| `CommandTimeoutSeconds` | Batch, query, diagnostic, and ALTER timeout. Default `600` |
| `MaxRetries` | Failed copies `001`–`004`, then the inbox file is removed |
| `JournalSchema` / `JournalTable` | DbUp journal on the target database. Default `[migration].[schema_versions]`. The poller creates the schema before the first script when it is missing. The login needs `CREATE SCHEMA`, or set `JournalSchema` to `dbo`. DbUp runs only when `DB_NAME()` / `current_database()` matches `Initial Catalog` / `Database`, then confirms the journal row on that database |

## Scripts

Put files in `scripts/inbox/ddl`, `dml`, `data`, or `query`. A first line `-- kind: ddl|dml|data|query` works when the file is not in one of those folders.

Typical SQL-project procedure script for the poller (also see [`scripts/inbox/ddl/002_example_create_or_alter_procedure.sql`](scripts/inbox/ddl/002_example_create_or_alter_procedure.sql)):

```sql
-- kind: ddl
CREATE OR ALTER PROCEDURE [dbo].[usp_Example]
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT @Id AS Id;
END
```

`CREATE PROCEDURE`, `CREATE PROC`, `CREATE FUNCTION`, `CREATE VIEW`, and `CREATE TRIGGER` are rewritten to `CREATE OR ALTER` on SQL Server (`CREATE OR REPLACE` on PostgreSQL) and run on every poll. Those files stay in the inbox. `CREATE TABLE` and other DDL still run once.

- Procedure, function, view, and trigger scripts stay in the inbox and run on every poll.
- Other DDL, DML, and data run once, are journaled, and move to `scripts/success`.
- Query scripts stay in the inbox and run every poll. Each run writes `scripts/success/{name}_{utc}.result.txt`.
- A failure copies `001_{file}.sql` and `001_{file}.error.txt` into `scripts/failed`.
- `GO` lines are removed from the file. Batches still run separately.
- A missing column runs a diagnostic query, adds the column, and retries that attempt once.

## DACPAC from SQL projects

Separate path for packaging desired-state `.sqlproj` models (not required to run inbox scripts). Build, extract, or publish a `.dacpac`:

```powershell
dotnet run --project src/MigrationTool.Host -- --dacpac-prereqs

dotnet run --project src/MigrationTool.Host -- --build-dacpac path\to\project.sqlproj --configuration Debug --output D:\dacpac-out

dotnet run --project src/MigrationTool.Host -- --extract-dacpac D:\out\live.dacpac

# Prefer --script-only before applying. Default blocks possible data loss; --allow-data-loss only with DBA approval.
dotnet run --project src/MigrationTool.Host -- --publish-dacpac D:\out\live.dacpac --script-only
dotnet run --project src/MigrationTool.Host -- --publish-dacpac D:\out\live.dacpac
```

`--build-dacpac` accepts a `.sqlproj` or a folder with exactly one `.sqlproj`. Path-only Ready script: [`../../tools/dacpac/Invoke-DacpacReady.ps1`](../../tools/dacpac/Invoke-DacpacReady.ps1) (`-CheckPrereqs`, `-Publish`, `-ScriptOnly`). SDK-style projects use `Microsoft.Build.Sql` (pinned in root `global.json`). Classic SSDT projects use Visual Studio MSBuild. Extract/publish use `SqlPackage.exe`.

SSDT **Build** scripts inside a `.sqlproj` must remain declarative (`CREATE PROCEDURE` / `CREATE TABLE`, one object per file, no `CREATE OR ALTER` / `GO`). That constraint applies only to dacpac builds — not to poller inbox scripts.
