---
description: Add a SQL script to the correct inbox folder
---

Add a SQL change script to the migration inbox (poller path — not dacpac Build).

1. Choose the kind: `ddl`, `dml`, `data`, or `query`.
2. Put the file in `src/MigrationTool.Host/scripts/inbox/{kind}/` with a sortable name such as `010_description.sql`.
3. For procedures/views/functions from a SQL Server project, prefer `CREATE OR ALTER …` so the script is safe to re-drop into the inbox. Plain `CREATE PROCEDURE` also works for a first-time apply.
4. If the script uses `GO`, leave those lines in place. The poller removes them and runs each batch.
5. Query scripts stay in the inbox and run every poll. DDL, DML, and data scripts move to success after one successful run.
6. Do not put passwords or connection strings in the script.
7. Do not confuse inbox scripts with `.sqlproj` SSDT Build objects (those require declarative `CREATE` without `CREATE OR ALTER` / `GO` for dacpac builds).
