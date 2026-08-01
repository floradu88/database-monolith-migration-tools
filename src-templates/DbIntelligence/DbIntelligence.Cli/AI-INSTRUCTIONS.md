# AI Instructions — `src-templates/DbIntelligence/DbIntelligence.Cli`

## Purpose

Command-line entry for prerequisite health checks and interactive installs (`--install-preqs`).

Node.js/npm for Angular are provisioned by PowerShell `../scripts/Initialize-DbIntelligenceNode.ps1` (fnm, user scope, no admin). **Codegraph** must be installed with **fnm when present**: `fnm exec -- npm i -g @colbymchenry/codegraph` (C# `--install-preqs` and the PowerShell helper both prefer this). Prefer that path over documenting admin Node MSI installs or bare system npm.

## Mandatory workflow

1. Do not invent production credentials.
2. Keep installs interactive unless `--yes` is explicitly passed.
3. Prefer documenting winget/pip/official installers over bundling binaries.
4. Assume operators may call `Install-DbIntelligencePrereqs.ps1` which runs Node/fnm setup before this CLI.

## Current files

- `DbIntelligence.Cli.csproj`
- `Program.cs`
- `appsettings.json`

## Completion report

Report changed files, assumptions, validation, unresolved risks, and required approval.
