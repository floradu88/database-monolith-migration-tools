#Requires -Version 5.1
<#
.SYNOPSIS
  Install / verify DbIntelligence CLI prerequisites (Python, Graphify, Codegraph).

.PARAMETER Yes
  Auto-confirm install prompts (non-interactive).

.EXAMPLE
  .\Install-DbIntelligencePrereqs.ps1
  .\Install-DbIntelligencePrereqs.ps1 -Yes
#>
[CmdletBinding()]
param(
    [Alias("AssumeYes")]
    [switch]$Yes
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$CliProject = Join-Path $Root "DbIntelligence.Cli\DbIntelligence.Cli.csproj"

function Test-Command([string]$Name) {
    return [bool](Get-Command $Name -ErrorAction SilentlyContinue)
}

Write-Host "Checking local toolchain..." -ForegroundColor Cyan
$dotnetOk = Test-Command "dotnet"
$nodeOk = Test-Command "node"
$npmOk = Test-Command "npm"
$pythonOk = (Test-Command "python") -or (Test-Command "py")

Write-Host "  dotnet : $(if ($dotnetOk) { 'OK' } else { 'MISSING' })"
Write-Host "  node   : $(if ($nodeOk) { 'OK' } else { 'MISSING' })"
Write-Host "  npm    : $(if ($npmOk) { 'OK' } else { 'MISSING' })"
Write-Host "  python : $(if ($pythonOk) { 'OK' } else { 'MISSING' })"

if (-not $dotnetOk) {
    throw ".NET SDK is required. Install from https://dotnet.microsoft.com/download"
}

# Prefer the managed CLI installer (prompts per missing tool).
$cliArgs = @("--install-preqs")
if ($Yes) { $cliArgs += "--yes" }

Write-Host "`nRunning DbIntelligence.Cli $($cliArgs -join ' ') ..." -ForegroundColor Cyan
dotnet run --project $CliProject -c Release -- @cliArgs
$exit = $LASTEXITCODE

Write-Host "`nRe-checking with --health ..." -ForegroundColor Cyan
dotnet run --project $CliProject -c Release -- --health
if ($LASTEXITCODE -ne 0) {
    Write-Warning "Health still unhealthy (exit $LASTEXITCODE). Install missing tools, open a new terminal so PATH refreshes, then re-run."
}

exit $exit
