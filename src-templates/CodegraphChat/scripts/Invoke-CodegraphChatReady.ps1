#Requires -Version 5.1
<#
.SYNOPSIS
  One command: install/check tools (fnm, no admin), build, publish SPA, start CodegraphChat.

.DESCRIPTION
  Only the project path is required. Everything else is auto-confirmed:
    1. User-scoped fnm Node + Codegraph via DbIntelligence helper (fnm env activation; Codegraph install uses fnm exec -- npm.cmd on Windows)
    2. Restore + build + test CodegraphChat.sln
    3. npm via activated fnm PATH (same as DbIntelligence — not `fnm exec -- npm`); Angular published to Api/wwwroot
    4. Start API on :5091 bound to the repository path (single-host UI at http://localhost:5091/)

.PARAMETER RepositoryPath
  Folder that has (or will get) a .codegraph index. Aliases: Path, ProjectPath, RepoPath.

.PARAMETER SkipWeb
  Skip Angular build / wwwroot publish (API only).

.PARAMETER SkipTests
  Skip dotnet test during build.

.EXAMPLE
  .\Invoke-CodegraphChatReady.ps1 "D:\path\to\your\app"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [Alias("Path", "ProjectPath", "RepoPath")]
    [string]$RepositoryPath,
    [switch]$SkipWeb,
    [switch]$SkipTests
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

Write-Host "=== CodegraphChat Ready (one command) ===" -ForegroundColor Cyan
Write-Host "Repository: $RepoFull"

if (-not (Test-Path $NodeInit)) {
    throw "Missing DbIntelligence Node helper at $NodeInit"
}

Write-Host "`n[1/3] fnm Node + Codegraph (no admin; fnm exec --using=lts-latest)..." -ForegroundColor Cyan
& $NodeInit -Install -InstallCodegraph -Yes
if ($LASTEXITCODE -ne 0) {
    Write-Warning "Node/Codegraph helper exited $LASTEXITCODE; continuing if tools are already on PATH."
}
. $NodeInit -Quiet

$codegraphOk = [bool](Get-Command codegraph -ErrorAction SilentlyContinue)
if (-not $codegraphOk) {
    throw "codegraph not on PATH. Prefer: fnm exec --using=lts-latest -- npm.cmd i -g @colbymchenry/codegraph  (Windows) or npm on non-Windows"
}

$npmCmd = Get-Command npm -ErrorAction SilentlyContinue
if ($npmCmd) {
    Write-Host "Using npm: $($npmCmd.Source)" -ForegroundColor DarkGray
}

$dotCodegraph = Join-Path $RepoFull ".codegraph"
if (-not (Test-Path $dotCodegraph)) {
    Write-Warning "No .codegraph under $RepoFull yet. Use Ensure index in the UI after start, or: codegraph init `"$RepoFull`"."
}

Write-Host "`n[2/3] Build (.NET + Angular via activated fnm npm -> Api/wwwroot)..." -ForegroundColor Cyan
$buildArgs = @{ Yes = $true }
if ($SkipWeb) { $buildArgs.SkipWeb = $true }
if ($SkipTests) { $buildArgs.SkipTests = $true }
& (Join-Path $Scripts "Build-CodegraphChat.ps1") @buildArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$wwwIndex = Join-Path $Root "CodegraphChat.Api\wwwroot\index.html"
Write-Host "`n[3/3] Start API..." -ForegroundColor Cyan
if (Test-Path $wwwIndex) {
    Write-Host "Open http://localhost:5091/  (SPA served from wwwroot)" -ForegroundColor Green
}
else {
    Write-Host "SPA not in wwwroot. API only on :5091. Optional: .\Start-CodegraphChatWeb.ps1 -Yes" -ForegroundColor Yellow
}

& (Join-Path $Scripts "Start-CodegraphChat.ps1") -Force -RepositoryPath $RepoFull
