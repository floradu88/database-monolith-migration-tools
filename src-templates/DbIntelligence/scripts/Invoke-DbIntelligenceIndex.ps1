#Requires -Version 5.1
<#
.SYNOPSIS
  Smoke-test indexing against a repository path (requires API running, or starts a one-shot job via CLI path).

.PARAMETER RepositoryPath
  Folder to index.

.PARAMETER ApiBase
  API base URL (default http://localhost:5088).

.PARAMETER SqlConnectionString
  When set, enables read-only SQL inventory (runSqlScan) and extracts live SPs into the map.

.PARAMETER UseShowcaseLocalDefaults
  Infer LocalDB Owned connection from Showcase appsettings.json (non-secret kit placeholders).

.EXAMPLE
  .\Invoke-DbIntelligenceIndex.ps1 -RepositoryPath "D:\code\projects\my-app"

.EXAMPLE
  .\Invoke-DbIntelligenceIndex.ps1 -RepositoryPath "D:\code\projects\my-app" -UseShowcaseLocalDefaults
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RepositoryPath,
    [string]$ApiBase = "http://localhost:5088",
    [string]$SqlConnectionString = "",
    [switch]$UseShowcaseLocalDefaults
)

$ErrorActionPreference = "Stop"
$full = (Resolve-Path $RepositoryPath).Path
. (Join-Path $PSScriptRoot "Resolve-DbIntelligenceSqlConnection.ps1")

$cs = Resolve-DbIntelligenceSqlConnection `
    -SqlConnectionString $SqlConnectionString `
    -UseShowcaseLocalDefaults:$UseShowcaseLocalDefaults
$runSql = -not [string]::IsNullOrWhiteSpace($cs)

Write-Host "Checking API health at $ApiBase/api/health ..." -ForegroundColor Cyan
try {
    $health = Invoke-RestMethod -Uri "$ApiBase/api/health" -Method Get
}
catch {
    throw "API is not reachable at $ApiBase. Start it with .\Start-DbIntelligence.ps1 first. $_"
}

Write-Host "Health status: $($health.status)"
if (-not $health.healthy) {
    Write-Warning $health.message
    Write-Warning $health.installHint
    throw "Prerequisites unhealthy. Run .\Install-DbIntelligencePrereqs.ps1"
}

$body = @{
    targetRepositoryPath = $full
    runCodegraph         = $true
    runGraphify          = $true
    runRepositoryScan    = $true
    runSqlScan           = $runSql
    sqlConnectionString  = $(if ($runSql) { $cs } else { $null })
} | ConvertTo-Json

Write-Host "Starting index job for $full (runSqlScan=$runSql) ..." -ForegroundColor Cyan
$job = Invoke-RestMethod -Uri "$ApiBase/api/index/jobs" -Method Post -Body $body -ContentType "application/json"
Write-Host "Job id: $($job.id)"

do {
    Start-Sleep -Seconds 2
    $status = Invoke-RestMethod -Uri "$ApiBase/api/index/jobs/$($job.id)" -Method Get
    Write-Host "[$($status.status)] $($status.phase) $($status.message)"
} while ($status.status -eq "Pending" -or $status.status -eq "Running")

if ($status.status -ne "Completed") {
    throw "Index job failed: $($status.message)"
}

$map = Invoke-RestMethod -Uri "$ApiBase/api/maps/code-to-db" -Method Get
$sp = Invoke-RestMethod -Uri "$ApiBase/api/maps/stored-procedures" -Method Get
Write-Host "code-to-db entries: $($map.entries.Count)" -ForegroundColor Green
Write-Host "stored procedures: $($sp.procedures.Count)" -ForegroundColor Green
exit 0
