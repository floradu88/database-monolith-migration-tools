#Requires -Version 5.1
<#
.SYNOPSIS
  Ready CodegraphChat against an already-mapped repository path.

.DESCRIPTION
  1. Ensure fnm Node + Codegraph (via DbIntelligence helper)
  2. Build .NET solution
  3. Run unit tests
  4. Start API bound to the repository path

.EXAMPLE
  .\Invoke-CodegraphChatReady.ps1 "D:\path\to\your\app"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$RepositoryPath,
    [switch]$SkipWebHint,
    [switch]$Yes
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Scripts = $PSScriptRoot
$DbIntelScripts = Join-Path (Split-Path -Parent $Root) "DbIntelligence\scripts"
$NodeInit = Join-Path $DbIntelScripts "Initialize-DbIntelligenceNode.ps1"
$Sln = Join-Path $Root "CodegraphChat.sln"

if (-not (Test-Path $RepositoryPath)) {
    throw "Repository path not found: $RepositoryPath"
}
$RepoFull = (Resolve-Path $RepositoryPath).Path

Write-Host "=== CodegraphChat Ready ===" -ForegroundColor Cyan
Write-Host "Repository: $RepoFull"

if (-not (Test-Path $NodeInit)) {
    throw "Missing DbIntelligence Node helper at $NodeInit"
}

Write-Host "`n[1/4] Node / Codegraph (fnm preferred)..." -ForegroundColor Cyan
if ($Yes) {
    & $NodeInit -Install -InstallCodegraph -Yes
}
else {
    & $NodeInit -Install -InstallCodegraph
}
. $NodeInit -Quiet

$codegraphOk = [bool](Get-Command codegraph -ErrorAction SilentlyContinue)
if (-not $codegraphOk) {
    throw "codegraph not on PATH. Re-run Initialize-DbIntelligenceNode.ps1 -InstallCodegraph -Yes"
}

$dotCodegraph = Join-Path $RepoFull ".codegraph"
if (-not (Test-Path $dotCodegraph)) {
    Write-Warning "No .codegraph folder under $RepoFull. Index first (DbIntelligence Ready, or: codegraph init `"$RepoFull`")."
}

Write-Host "`n[2/4] Build..." -ForegroundColor Cyan
dotnet build $Sln -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n[3/4] Tests..." -ForegroundColor Cyan
dotnet test $Sln -c Release --no-build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n[4/4] Start API..." -ForegroundColor Cyan
if (-not $SkipWebHint) {
    Write-Host "In another terminal: .\Start-CodegraphChatWeb.ps1" -ForegroundColor DarkGray
    Write-Host "UI: http://localhost:4201  API: http://localhost:5091" -ForegroundColor DarkGray
}

& (Join-Path $Scripts "Start-CodegraphChat.ps1") -Force -RepositoryPath $RepoFull
