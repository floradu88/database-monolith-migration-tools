#Requires -Version 5.1
<#
.SYNOPSIS
  Master setup for DbIntelligence (prereqs + restore + build + test + health).

.PARAMETER SkipPrereqs
  Skip prerequisite installation prompts.

.PARAMETER Yes
  Auto-confirm prerequisite install prompts.

.PARAMETER SkipWeb
  Skip npm install / Angular restore.

.EXAMPLE
  .\Setup-DbIntelligence.ps1
  .\Setup-DbIntelligence.ps1 -Yes
#>
[CmdletBinding()]
param(
    [switch]$SkipPrereqs,
    [switch]$Yes,
    [switch]$SkipWeb
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$RepoRoot = Resolve-Path (Join-Path $Root "..\..")
$Scripts = $PSScriptRoot

Write-Host "=== DbIntelligence setup ===" -ForegroundColor Cyan
Write-Host "DbIntelligence root: $Root"
Write-Host "Repo root:           $RepoRoot"

if (-not $SkipPrereqs) {
    Write-Host "`n--- Prerequisites ---" -ForegroundColor Cyan
    $prereqArgs = @()
    if ($Yes) { $prereqArgs += "-Yes" }
    & (Join-Path $Scripts "Install-DbIntelligencePrereqs.ps1") @prereqArgs
    if ($LASTEXITCODE -ne 0 -and -not $Yes) {
        Write-Warning "Prerequisite step exited $LASTEXITCODE. Continuing with build; fix missing tools if health fails."
    }
}

Write-Host "`n--- Build & test ---" -ForegroundColor Cyan
& (Join-Path $Scripts "Build-DbIntelligence.ps1") -SkipWeb:$SkipWeb
if ($LASTEXITCODE -ne 0) {
    throw "Build/test failed with exit code $LASTEXITCODE"
}

Write-Host "`n--- Health check ---" -ForegroundColor Cyan
& (Join-Path $Scripts "Test-DbIntelligenceHealth.ps1")
$healthExit = $LASTEXITCODE

Write-Host "`n=== Setup complete ===" -ForegroundColor Green
Write-Host "Next:"
Write-Host "  .\scripts\Start-DbIntelligence.ps1 -Force"
Write-Host "  .\scripts\Start-DbIntelligenceWeb.ps1"
Write-Host "  .\scripts\Invoke-DbIntelligenceIndex.ps1 -RepositoryPath `"D:\path\to\repo`""
Write-Host "  .\scripts\Invoke-DbIntelligenceBatchIndex.ps1 -ParentFolderPath `"D:\code\projects`""
Write-Host "  .\scripts\Invoke-DbIntelligenceBatchIndex.ps1 -ParentFolderPath `"C:\code`""
Write-Host "Node tip: .\scripts\Initialize-DbIntelligenceNode.ps1 -Install -Yes  (fnm Node + Codegraph via fnm exec)"

exit $healthExit
