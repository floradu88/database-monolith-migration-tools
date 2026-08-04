#Requires -Version 5.1
<#
.SYNOPSIS
  Start CodegraphChat.Api on http://localhost:5091.

.EXAMPLE
  .\Start-CodegraphChat.ps1 -Force
  .\Start-CodegraphChat.ps1 -Force -RepositoryPath "D:\path\to\your\app"
#>
[CmdletBinding()]
param(
    [switch]$Force,
    [int]$Port = 5091,
    [string]$RepositoryPath = ""
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Api = Join-Path $Root "CodegraphChat.Api"
$NodeInit = Join-Path (Split-Path -Parent $Root) "DbIntelligence\scripts\Initialize-DbIntelligenceNode.ps1"

if (Test-Path $NodeInit) {
    . $NodeInit -Quiet
}

if ($Force) {
    $listeners = Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty OwningProcess -Unique
    foreach ($procId in $listeners) {
        if ($procId -and $procId -ne 0) {
            Write-Host "Stopping process $procId on port $Port..." -ForegroundColor Yellow
            Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
        }
    }
}

$env:ASPNETCORE_URLS = "http://localhost:$Port"
if ($RepositoryPath) {
    $env:CodegraphChat__TargetRepositoryPath = (Resolve-Path $RepositoryPath).Path
}

Write-Host "Starting CodegraphChat.Api on http://localhost:$Port ..." -ForegroundColor Cyan
Push-Location $Api
try {
    dotnet run --project .\CodegraphChat.Api.csproj --no-launch-profile
}
finally {
    Pop-Location
}
