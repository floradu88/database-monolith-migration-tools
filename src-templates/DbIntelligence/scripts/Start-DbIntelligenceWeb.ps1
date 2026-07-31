#Requires -Version 5.1
<#
.SYNOPSIS
  Start Angular DbIntelligence.Web (http://localhost:4200, proxies /api -> :5088).

.EXAMPLE
  .\Start-DbIntelligenceWeb.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Web = Join-Path $Root "DbIntelligence.Web"

if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
    throw "npm is required. Install Node.js 18+."
}

Push-Location $Web
try {
    if (-not (Test-Path "node_modules")) {
        Write-Host "Installing npm packages..." -ForegroundColor Cyan
        npm install
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    Write-Host "Starting Angular on http://localhost:4200 ..." -ForegroundColor Cyan
    npm start
}
finally {
    Pop-Location
}
