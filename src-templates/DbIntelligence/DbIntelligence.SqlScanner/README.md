# DbIntelligence.SqlScanner

Read-only SQL Server object inventory helper for the evidence graph. Uses configuration / user secrets for connection strings — never invent production credentials.

Skipped when `runSqlScan` is false or the connection string empty (expected in most local runs).

PowerShell helpers:

- `../scripts/Resolve-DbIntelligenceSqlConnection.ps1` — resolve CS / Showcase LocalDB placeholders
- `../scripts/Invoke-DbIntelligenceExtractSps.ps1` — enable `runSqlScan` and print the SP map

Parent how-to: [`../README.md`](../README.md) · [`../../../HOW-TO-USE.md`](../../../HOW-TO-USE.md).
