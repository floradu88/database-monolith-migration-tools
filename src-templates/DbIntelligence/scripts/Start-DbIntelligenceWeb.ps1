#Requires -Version 5.1
<#
.SYNOPSIS
  Start Angular DbIntelligence.Web (http://localhost:4200, proxies /api -> :5088).

.DESCRIPTION
  Activates user-scoped fnm Node/npm when present. If missing, installs via
  Initialize-DbIntelligenceNode.ps1 (winget fnm --scope user - no admin).

.EXAMPLE
  .\Start-DbIntelligenceWeb.ps1
#>
[CmdletBinding()]
param(
    [switch]$Yes
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Web = Join-Path $Root "DbIntelligence.Web"
$NodeInit = Join-Path $PSScriptRoot "Initialize-DbIntelligenceNode.ps1"

. $NodeInit -Quiet
if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
    Write-Host "npm not found - installing user-scoped Node via fnm (no admin)..." -ForegroundColor Yellow
    if ($Yes) {
        & $NodeInit -Install -Yes
    }
    else {
        & $NodeInit -Install
    }
    if ($LASTEXITCODE -ne 0) {
        throw "npm is required. Run .\Initialize-DbIntelligenceNode.ps1 -Install -Yes"
    }
    . $NodeInit -Quiet
}

if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
    throw "npm is required. Run .\Initialize-DbIntelligenceNode.ps1 -Install -Yes"
}

$npmCmd = if (Get-Command npm.cmd -ErrorAction SilentlyContinue) { "npm.cmd" } else { "npm" }
if (Get-Variable -Name PSNativeCommandUseErrorActionPreference -ErrorAction SilentlyContinue) {
    $PSNativeCommandUseErrorActionPreference = $false
}

Push-Location $Web
try {
    if (-not (Test-Path "node_modules")) {
        Write-Host "Installing npm packages..." -ForegroundColor Cyan
        & $npmCmd install
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    Write-Host "Starting Angular on http://localhost:4200 ..." -ForegroundColor Cyan
    Write-Host "Using $npmCmd : $((Get-Command $npmCmd).Source)" -ForegroundColor DarkGray
    & $npmCmd start
}
finally {
    Pop-Location
}
