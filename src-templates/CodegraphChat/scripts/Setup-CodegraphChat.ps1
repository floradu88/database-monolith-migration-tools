#Requires -Version 5.1
<#
.SYNOPSIS
  Setup CodegraphChat (fnm/Codegraph via DbIntelligence helper + build; does not start the API).

.PARAMETER Yes
  Auto-confirm Node/fnm/Codegraph install prompts.

.PARAMETER SkipWeb
  Skip Angular build / wwwroot publish.

.PARAMETER SkipTests
  Skip dotnet test during build.

.EXAMPLE
  .\Setup-CodegraphChat.ps1 -Yes
#>
[CmdletBinding()]
param(
    [switch]$Yes,
    [switch]$SkipWeb,
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Scripts = $PSScriptRoot
$DbIntelScripts = Join-Path (Split-Path -Parent $Root) "DbIntelligence\scripts"
$NodeInit = Join-Path $DbIntelScripts "Initialize-DbIntelligenceNode.ps1"

Write-Host "=== CodegraphChat setup ===" -ForegroundColor Cyan
Write-Host "Root: $Root"

if (-not (Test-Path $NodeInit)) {
    throw "Missing DbIntelligence Node helper: $NodeInit"
}

Write-Host "`n--- Prerequisites (fnm Node + Codegraph) ---" -ForegroundColor Cyan
if ($Yes) {
    & $NodeInit -Install -InstallCodegraph -Yes
}
else {
    & $NodeInit -Install -InstallCodegraph
}
. $NodeInit -Quiet

if (-not (Get-Command codegraph -ErrorAction SilentlyContinue)) {
    Write-Warning "codegraph not on PATH after install. Re-run Initialize-DbIntelligenceNode.ps1 -InstallCodegraph -Yes"
}

Write-Host "`n--- Build ---" -ForegroundColor Cyan
$buildArgs = @{}
if ($SkipWeb) { $buildArgs.SkipWeb = $true }
if ($SkipTests) { $buildArgs.SkipTests = $true }
if ($Yes) { $buildArgs.Yes = $true }

& (Join-Path $Scripts "Build-CodegraphChat.ps1") @buildArgs
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE"
}

Write-Host "`nSetup complete. Start with:" -ForegroundColor Green
Write-Host "  .\Start-CodegraphChat.ps1 -Force -RepositoryPath `"D:\path\to\your\app`""
Write-Host "  (optional hot-reload UI) .\Start-CodegraphChatWeb.ps1"
exit 0
