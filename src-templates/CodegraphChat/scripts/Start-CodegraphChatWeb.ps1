#Requires -Version 5.1
<#
.SYNOPSIS
  Start Angular CodegraphChat.Web (http://localhost:4201, proxies /api -> :5091).

.DESCRIPTION
  Activates user-scoped fnm Node (DbIntelligence helper), then runs npm directly —
  same pattern as Start-DbIntelligenceWeb.ps1. Do not use `fnm exec -- npm` on Windows.

.EXAMPLE
  .\Start-CodegraphChatWeb.ps1
  .\Start-CodegraphChatWeb.ps1 -Yes
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

foreach ($name in @("npm_config_devdir", "NPM_CONFIG_DEVDIR")) {
    if (Test-Path "Env:$name") {
        Remove-Item "Env:$name" -ErrorAction SilentlyContinue
    }
}

if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
    throw "npm is required. Run ..\DbIntelligence\scripts\Initialize-DbIntelligenceNode.ps1 -Install -Yes"
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

    Write-Host "Starting Angular on http://localhost:4201 ..." -ForegroundColor Cyan
    Write-Host "Using $npmCmd : $((Get-Command $npmCmd).Source)" -ForegroundColor DarkGray
    & $npmCmd start
}
finally {
    Pop-Location
}
