#Requires -Version 5.1
<#
.SYNOPSIS
  Run DbIntelligence CLI health check (python / pip / graphify / codegraph).

.EXAMPLE
  .\Test-DbIntelligenceHealth.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$CliProject = Join-Path $Root "DbIntelligence.Cli\DbIntelligence.Cli.csproj"

Write-Host "DbIntelligence health check..." -ForegroundColor Cyan
dotnet run --project $CliProject -c Release -- --health
exit $LASTEXITCODE
