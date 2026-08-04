#Requires -Version 5.1
<#
.SYNOPSIS
  Start Angular CodegraphChat.Web (http://localhost:4201, proxies /api -> :5091).

.DESCRIPTION
  Reuses DbIntelligence user-scoped fnm Node helper (no admin).

.EXAMPLE
  .\Start-CodegraphChatWeb.ps1
#>
[CmdletBinding()]
param(
    [switch]$Yes
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Web = Join-Path $Root "CodegraphChat.Web"
$NodeInit = Join-Path (Split-Path -Parent $Root) "DbIntelligence\scripts\Initialize-DbIntelligenceNode.ps1"

if (-not (Test-Path $NodeInit)) {
    throw "Missing DbIntelligence Node helper: $NodeInit"
}

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
        throw "npm is required. Run ..\DbIntelligence\scripts\Initialize-DbIntelligenceNode.ps1 -Install -Yes"
    }
    . $NodeInit -Quiet
}

if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
    throw "npm is required. Run ..\DbIntelligence\scripts\Initialize-DbIntelligenceNode.ps1 -Install -Yes"
}

Push-Location $Web
try {
    if (-not (Test-Path "node_modules")) {
        Write-Host "Installing npm packages..." -ForegroundColor Cyan
        npm install
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    Write-Host "Starting Angular on http://localhost:4201 ..." -ForegroundColor Cyan
    Write-Host "Using npm: $((Get-Command npm).Source)" -ForegroundColor DarkGray
    npm start
}
finally {
    Pop-Location
}
