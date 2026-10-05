# Claude Code — SQL DB Modernization Kit

Use this repository as a production-grade kit for decomposing a SQL Server monolith into independently owned data services and databases.

## Before any change

1. Read `README.md`, `HOW-TO-USE.md`, `REVIEW-REPORT.md`, and `docs/CANONICAL-DOCUMENT-INDEX.md`.
2. Read the nearest `AI-INSTRUCTIONS.md` for the folder you will edit (see `AI-INSTRUCTION-INDEX.md`).
3. Prefer canonical docs listed in the root README “Start here” section over supplemental/legacy docs.


## Safety rules

- Preserve ownership boundaries; prefer additive, reversible changes.
- Do not invent credentials, approvals, platform capabilities, or production values.
- Never execute destructive SQL automatically.
- Do not apply SQL scripts to production without DBA review.
- Keep SQL project ownership and EF Core migrations ownership non-overlapping.
- When behavior changes, update docs, manifests, tests, RBAC, observability, and rollback guidance together.

## Kit layout

| Path | Role |
|------|------|
| `docs/` | Architecture, migration plan, runbooks (canonical + supplemental) |
| `sql/` | Discovery, telemetry, audit, RBAC, and platform SQL scripts |
| `manifests/` | Domain ownership and migration-wave examples |
| `src-templates/` | .NET solution/project scaffolds (source split, data services, control plane) |
| `src-templates/DbIntelligence/scripts/` | PowerShell setup/run; prefer `Invoke-DbIntelligenceReady.ps1` (path only); fnm Node + Codegraph via `fnm exec --using=lts-latest` |
| `src-templates/CodegraphChat/scripts/` | Topic chat Ready: `Invoke-CodegraphChatReady.ps1` (path only) → http://localhost:5091/ |
| `tools/yaml-topology/` | Recursive YAML → Mermaid topology Markdown (`Invoke-YamlTopologyReady.ps1` path only) |
| `tools/dacpac/` | Build/extract `.dacpac` from `.sqlproj` / SqlPackage (`Invoke-DacpacReady.ps1` path only) |
| `checklists/` | Cutover and source-split checklists |
| `validation/` | Checksums and validation summary |
| `HOW-TO-USE.md` | Canonical PowerShell command reference (DbIntelligence + CodegraphChat + YAML Topology + DACPAC) |
| `src/MigrationTool.Host` | Hangfire worker that applies inbox SQL scripts with DbUp (+ `--build-dacpac`) |

## Completion report

When finishing work, report:

- changed files;
- assumptions;
- validation performed;
- unresolved risks;
- required human approvals.

## SQL script poller

How to run: [`src/MigrationTool.Host/README.md`](src/MigrationTool.Host/README.md) and [`HOW-TO-USE.md`](HOW-TO-USE.md).

**Primary path for “clone → set connection string → drop SQL scripts → run on DB.”** Prefer `CREATE OR ALTER PROCEDURE` (and other `CREATE OR ALTER …`) in inbox scripts; that is supported. Do not confuse this with dacpac SSDT Build scripts (those stay declarative `CREATE …`).

.NET 10 worker (`net10.0`). `global.json` accepts the local 10.0 SDK (`10.0.203`) and newer 10.0 SDKs, including `10.0.401`. Hangfire polls `Migration:InboxPath` and applies SQL with DbUp to SQL Server or PostgreSQL.

- Inbox: scripts waiting to run. Subfolders `ddl`, `dml`, `data`, and `query` set the script kind (or first line `-- kind: …`).
- Success: applied DDL, DML, and data scripts, query result files, and ALTER pre-steps.
- Failed: `001_script.sql` and `001_script.error.txt` through attempt `004`.

Query scripts stay in the inbox and run every poll. Other scripts run once and are journaled on the target database.

```powershell
git clone https://github.com/floradu88/database-monolith-migration-tools.git
cd database-monolith-migration-tools
$env:MIGRATION_CONNECTION_STRING = "Data Source=mydb.abc123.us-east-1.rds.amazonaws.com,1433;Initial Catalog=YourDb;User ID=admin;Password=***;Encrypt=True;TrustServerCertificate=False;"
# copy *.sql into src/MigrationTool.Host/scripts/inbox/ddl/
dotnet run --project src/MigrationTool.Host -- --check-network
dotnet run --project src/MigrationTool.Host -- --validate-connection
dotnet run --project src/MigrationTool.Host -- --poll-once
```

The Hangfire dashboard is `http://127.0.0.1:5088/hangfire` when the host is running. Hangfire storage is in-memory. The DbUp journal is the table named by `JournalSchema` and `JournalTable` on the target database.

Set `MIGRATION_CONNECTION_STRING` or `Migration:ConnectionString`. See `appsettings.example.json` for Azure SQL and managed PostgreSQL connection strings. Do not commit secrets.

`CommandTimeoutSeconds` applies to change batches, queries, diagnostics, and ALTER pre-steps. Default is 600.

A missing column runs a diagnostic query, adds the column, and retries once in that attempt.

## DACPAC from SQL projects

Separate from the inbox poller. Use when you need a `.dacpac` model (build/extract/publish), not when you only want to run exported `.sql` scripts.

```powershell
dotnet run --project src/MigrationTool.Host -- --dacpac-prereqs
dotnet run --project src/MigrationTool.Host -- --build-dacpac path\to\project.sqlproj --output D:\dacpac-out
dotnet run --project src/MigrationTool.Host -- --publish-dacpac D:\out\app.dacpac --script-only
# or
.\tools\dacpac\Invoke-DacpacReady.ps1 -CheckPrereqs
.\tools\dacpac\Invoke-DacpacReady.ps1 "path\to\project.sqlproj"
```

SDK-style projects use `Microsoft.Build.Sql` (pinned in `global.json`). Classic SSDT uses Visual Studio MSBuild. Extract/publish use SqlPackage. Prefer `--script-only` before publish; default blocks possible data loss. SSDT Build objects must remain `CREATE …` (no `CREATE OR ALTER` / `GO` in Build files).
