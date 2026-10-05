---
description: Run one migration inbox poll and report the result
---

Run one poll cycle of the migration tool from the repo root (apply inbox `.sql` to the target database).

1. Confirm `src/MigrationTool.Host/appsettings.json` or `MIGRATION_CONNECTION_STRING` points at the intended database. Do not print the connection string.
2. Confirm scripts are under `src/MigrationTool.Host/scripts/inbox/ddl|dml|data|query` (or have `-- kind: …`). `CREATE OR ALTER` is allowed and preferred for procedures.
3. Run `dotnet run --project src/MigrationTool.Host -- --poll-once`.
4. Report processed, succeeded, failed, and repaired counts from the log.
5. If a script failed, read the newest `*.error.txt` under the configured failed folder and summarize the database message.
