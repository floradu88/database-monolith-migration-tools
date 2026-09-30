---
description: Build a .dacpac from a .sqlproj using VS / .NET DAC tools
---

Build a DACPAC from a SQL project (does not publish).

1. Prefer `tools/dacpac/Invoke-DacpacReady.ps1 -CheckPrereqs`, then a `.sqlproj` path or `tools/dacpac/fixtures/DacpacFixture`.
2. Or from repo root: `dotnet run --project src/MigrationTool.Host -- --dacpac-prereqs` then `--build-dacpac <path.sqlproj|folder> --output <dir>`.
3. For extract-only from a database (DBA-approved connection): `--extract-dacpac <out.dacpac>` with `MIGRATION_CONNECTION_STRING`. Do not print the connection string.
4. Report the resulting `.dacpac` path and whether the project was SDK-style (`Microsoft.Build.Sql`) or classic SSDT.
