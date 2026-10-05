---
description: Check DNS, ping, and TCP reachability to the migration database host
---

Probe network reachability for the configured database host without opening a SQL login.

1. Prefer `MIGRATION_CONNECTION_STRING` or `Migration:ConnectionString`. Do not print the password.
2. Run `dotnet run --project src/MigrationTool.Host -- --check-network` (alias: `--ping`).
3. Optional: `--connection "<string>"` to override for this check only.
4. Report DNS resolve, ICMP ping (optional; often blocked on AWS), and TCP connect to the DB port. TCP must succeed; ICMP failure alone is not a hard fail.
