---
description: Add a SQL script to the correct inbox folder
---

Add a SQL change script to the migration inbox.

1. Choose the kind: `ddl`, `dml`, `data`, or `query`.
2. Put the file in `src/MigrationTool.Host/scripts/inbox/{kind}/` with a sortable name such as `010_description.sql`.
3. If the script uses `GO`, leave those lines in place. The poller removes them and runs each batch.
4. Query scripts stay in the inbox and run every poll. DDL, DML, and data scripts move to success after one successful run.
5. Do not put passwords or connection strings in the script.
