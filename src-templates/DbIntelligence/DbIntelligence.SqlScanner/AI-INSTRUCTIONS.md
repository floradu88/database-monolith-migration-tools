# AI Instructions — `src-templates/DbIntelligence/DbIntelligence.SqlScanner`

## Purpose

Read-only SQL Server object inventory and dependency extraction for the evidence graph.

## Mandatory workflow

1. Read parent `DbIntelligence/AI-INSTRUCTIONS.md` and root kit docs.
2. Keep queries read-only; never execute destructive SQL.
3. Do not invent production connection strings; use configuration/user secrets.
4. Prefer inventory patterns consistent with `sql/common/04-inventory-objects-and-dependencies.sql`.

## Current files

- `DbIntelligence.SqlScanner.csproj`
- `SqlScannerService.cs`
- `README.md`
- `AI-INSTRUCTIONS.md`

## Completion report

Report changed files, assumptions, validation, unresolved risks, and required approval.
