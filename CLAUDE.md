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
| `checklists/` | Cutover and source-split checklists |
| `validation/` | Checksums and validation summary |
| `HOW-TO-USE.md` | Canonical PowerShell command reference (DbIntelligence + CodegraphChat + YAML Topology) |
| `src/MigrationTool.Host` | Hangfire worker that applies inbox SQL scripts with DbUp |

## Completion report

When finishing work, report:

- changed files;
- assumptions;
- validation performed;
- unresolved risks;
- required human approvals.

## SQL script poller

.NET 9 worker. Hangfire polls `Migration:InboxPath` and applies SQL with DbUp to SQL Server or PostgreSQL.

- Inbox: scripts waiting to run. Subfolders `ddl`, `dml`, `data`, and `query` set the script kind.
- Success: applied DDL, DML, and data scripts, query result files, and ALTER pre-steps.
- Failed: `001_script.sql` and `001_script.error.txt` through attempt `004`.

Query scripts stay in the inbox and run every poll. Other scripts run once and are journaled on the target database.

One poll cycle:

```powershell
dotnet run --project src/MigrationTool.Host -- --poll-once
```

The Hangfire dashboard is `http://127.0.0.1:5088/hangfire` when the host is running. Hangfire storage is in-memory. The DbUp journal is the table named by `JournalSchema` and `JournalTable` on the target database.

Set `MIGRATION_CONNECTION_STRING` or `Migration:ConnectionString`. See `appsettings.example.json` for Azure SQL and managed PostgreSQL connection strings. Do not commit secrets.

`CommandTimeoutSeconds` applies to change batches, queries, diagnostics, and ALTER pre-steps. Default is 600.

A missing column runs a diagnostic query, adds the column, and retries once in that attempt.
