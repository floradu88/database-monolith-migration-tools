---
description: Build a .dacpac from a .sqlproj using VS / .NET DAC tools
---

Build a DACPAC from a SQL project (does not publish).

1. Prefer `tools/dacpac/Invoke-DacpacReady.ps1 -CheckPrereqs`, then a `.sqlproj` path or `tools/dacpac/fixtures/DacpacFixture`.
2. Or from repo root: `dotnet run --project src/MigrationTool.Host -- --dacpac-prereqs` then `--build-dacpac <path.sqlproj|folder> --output <dir>`.
3. For extract: `--extract-dacpac <out.dacpac>`. For publish: prefer `--publish-dacpac <file.dacpac> --script-only` before applying; default blocks possible data loss (`--allow-data-loss` only with DBA approval). Use `MIGRATION_CONNECTION_STRING`. Do not print the connection string.
4. Report the resulting `.dacpac` / script path and whether the project was SDK-style (`Microsoft.Build.Sql`) or classic SSDT.
