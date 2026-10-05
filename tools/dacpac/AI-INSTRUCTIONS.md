# AI Instructions — `tools/dacpac`

## Purpose

Operator wrapper around Visual Studio / .NET DAC tooling to **build**, **extract**, or **publish** `.dacpac` files from `.sqlproj` / SqlPackage. Prefer `--script-only` before live publish.

## Mandatory workflow

1. Prefer `Invoke-DacpacReady.ps1` (path to `.sqlproj` or fixture folder).
2. To install tooling without the Visual Studio IDE, use `Install-DacpacTools.ps1` (SqlPackage; optional VS Build Tools + DataBuildTools for classic SSDT). Never install full VS IDE from this script.
3. Prefer script-only publish before applying; do not invent production connection strings or auto-publish to production.
4. When changing behavior, update `README.md`, root `HOW-TO-USE.md`, and MigrationTool Host docs/commands together.
5. Keep SQL project ownership separate from EF Core migrations ownership.

## Completion report

Report changed files, assumptions, validation, unresolved risks, and required approvals.
