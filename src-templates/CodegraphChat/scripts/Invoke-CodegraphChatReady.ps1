#Requires -Version 5.1
<#
.SYNOPSIS
  Ready CodegraphChat against a repository path (build + start API).

.DESCRIPTION
  1. User-scoped fnm Node + Codegraph (via DbIntelligence helper)
  2. Build .NET + Angular (publish SPA to Api/wwwroot) unless -SkipWeb
  3. Start API bound to the repository path (single-host UI at :5091 when wwwroot present)

.EXAMPLE
  .\Invoke-CodegraphChatReady.ps1 "D:\path\to\your\app"
  .\Invoke-CodegraphChatReady.ps1 "D:\path\to\your\app" -SkipWeb
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$RepositoryPath,
    [switch]$SkipWeb,
    [switch]$SkipWebHint,
    [switch]$Yes
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Scripts = $PSScriptRoot
$DbIntelScripts = Join-Path (Split-Path -Parent $Root) "DbIntelligence\scripts"
$NodeInit = Join-Path $DbIntelScripts "Initialize-DbIntelligenceNode.ps1"

if (-not (Test-Path $RepositoryPath)) {
    throw "Repository path not found: $RepositoryPath"
}
$RepoFull = (Resolve-Path $RepositoryPath).Path

Write-Host "=== CodegraphChat Ready ===" -ForegroundColor Cyan
Write-Host "Repository: $RepoFull"

if (-not (Test-Path $NodeInit)) {
    throw "Missing DbIntelligence Node helper at $NodeInit"
}

Write-Host "`n[1/3] Node / Codegraph (fnm preferred)..." -ForegroundColor Cyan
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
    Write-Warning "No .codegraph folder under $RepoFull. Use Ensure index in the UI, or: codegraph init `"$RepoFull`"."
}

Write-Host "`n[2/3] Build..." -ForegroundColor Cyan
$buildArgs = @{ Yes = $true }
if ($SkipWeb) { $buildArgs.SkipWeb = $true }
if ($Yes) { $buildArgs.Yes = $true }
& (Join-Path $Scripts "Build-CodegraphChat.ps1") @buildArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$wwwIndex = Join-Path $Root "CodegraphChat.Api\wwwroot\index.html"
Write-Host "`n[3/3] Start API..." -ForegroundColor Cyan
if (Test-Path $wwwIndex) {
    Write-Host "Single-host UI: http://localhost:5091/" -ForegroundColor DarkGray
}
elseif (-not $SkipWebHint) {
    Write-Host "In another terminal: .\Start-CodegraphChatWeb.ps1" -ForegroundColor DarkGray
    Write-Host "UI: http://localhost:4201  API: http://localhost:5091" -ForegroundColor DarkGray
}

& (Join-Path $Scripts "Start-CodegraphChat.ps1") -Force -RepositoryPath $RepoFull
