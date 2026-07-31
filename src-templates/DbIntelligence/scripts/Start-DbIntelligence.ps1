#Requires -Version 5.1
<#
.SYNOPSIS
  Start DbIntelligence.Api (http://localhost:5088 by default).

.PARAMETER RepositoryPath
  Optional default target repository path written into a temporary appsettings overlay.

.PARAMETER Force
  Stop any process already listening on the API port before starting.

.EXAMPLE
  .\Start-DbIntelligence.ps1
  .\Start-DbIntelligence.ps1 -RepositoryPath "D:\code\projects\my-app"
  .\Start-DbIntelligence.ps1 -Force
#>
[CmdletBinding()]
param(
    [string]$RepositoryPath = "",
    [int]$Port = 5088,
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$ApiProject = Join-Path $Root "DbIntelligence.Api\DbIntelligence.Api.csproj"

function Get-ListenersOnPort([int]$PortNumber) {
    try {
        return @(Get-NetTCPConnection -LocalPort $PortNumber -State Listen -ErrorAction SilentlyContinue)
    }
    catch {
        return @()
    }
}

$listeners = Get-ListenersOnPort $Port
if ($listeners.Count -gt 0) {
    $pids = $listeners | Select-Object -ExpandProperty OwningProcess -Unique
    if ($Force) {
        foreach ($procId in $pids) {
            Write-Warning "Stopping PID $procId listening on port $Port"
            Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
        }
        Start-Sleep -Seconds 1
    }
    else {
        throw "Port $Port is already in use (PID(s): $($pids -join ', ')). Re-run with -Force to stop it, or use another port."
    }
}

$env:ASPNETCORE_ENVIRONMENT = "Development"
if ($RepositoryPath) {
    $full = (Resolve-Path $RepositoryPath).Path
    $env:DbIntelligence__TargetRepositoryPath = $full
    Write-Host "TargetRepositoryPath = $full"
}

Write-Host "Starting DbIntelligence.Api on http://localhost:$Port ..." -ForegroundColor Cyan
Write-Host "Health: http://localhost:$Port/api/health"
Write-Host "Tools:  http://localhost:$Port/api/tools"
dotnet run --project $ApiProject -c Release --urls "http://localhost:$Port"
