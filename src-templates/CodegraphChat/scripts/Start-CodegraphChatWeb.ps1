#Requires -Version 5.1
<#
.SYNOPSIS
  Start Angular CodegraphChat.Web (http://localhost:4201, proxies /api -> :5091).

.DESCRIPTION
  Activates user-scoped fnm Node (DbIntelligence helper). Prefers
  fnm exec --using=lts-latest -- npm for install/start when fnm is present.

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

function Invoke-KitNpm {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$NpmArgs)
    if (Get-Command fnm -ErrorAction SilentlyContinue) {
        Write-Host "fnm exec --using=lts-latest -- npm $($NpmArgs -join ' ')" -ForegroundColor DarkGray
        & fnm exec --using=lts-latest -- npm @NpmArgs
        return $LASTEXITCODE
    }
    & npm @NpmArgs
    return $LASTEXITCODE
}

if (-not (Get-Command npm -ErrorAction SilentlyContinue) -and -not (Get-Command fnm -ErrorAction SilentlyContinue)) {
    throw "npm is required. Run ..\DbIntelligence\scripts\Initialize-DbIntelligenceNode.ps1 -Install -Yes"
}

Push-Location $Web
try {
    if (-not (Test-Path "node_modules")) {
        Write-Host "Installing npm packages (prefer fnm)..." -ForegroundColor Cyan
        $code = Invoke-KitNpm @("install")
        if ($code -ne 0) { exit $code }
    }

    Write-Host "Starting Angular on http://localhost:4201 ..." -ForegroundColor Cyan
    if (Get-Command fnm -ErrorAction SilentlyContinue) {
        & fnm exec --using=lts-latest -- npm start
    }
    else {
        Write-Host "Using npm: $((Get-Command npm).Source)" -ForegroundColor DarkGray
        npm start
    }
}
finally {
    Pop-Location
}
