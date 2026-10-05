---
description: Validate the migration SQL connection string
---

Validate the configured (or overridden) database connection before polling.

1. Prefer `MIGRATION_CONNECTION_STRING` or `Migration:ConnectionString`. For AWS RDS SQL Server use `Data Source=host,1433;Initial Catalog=...;User ID=...;Password=...;Encrypt=True;` (comma before 1433). Do not print the password.
2. Optional first: `dotnet run --project src/MigrationTool.Host -- --check-network` (or `--ping`) for DNS + ICMP (optional) + TCP port without SQL login.
3. Run `dotnet run --project src/MigrationTool.Host -- --validate-connection` for shape + network + live open.
4. Optional: `--connection "<string>"` to override for this check only.
5. Report DNS/TCP/ping results and whether the live open succeeded. If it failed, report the classified kind (login/network/timeout/firewall/TLS) and guidance without echoing secrets.
