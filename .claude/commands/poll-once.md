---
description: Run one migration inbox poll and report the result
---

Run one poll cycle of the migration tool from the repo root.

1. Confirm `src/MigrationTool.Host/appsettings.json` or `MIGRATION_CONNECTION_STRING` points at the intended database. Do not print the connection string.
2. Run `dotnet run --project src/MigrationTool.Host -- --poll-once`.
3. Report processed, succeeded, failed, and repaired counts from the log.
4. If a script failed, read the newest `*.error.txt` under the configured failed folder and summarize the database message.
