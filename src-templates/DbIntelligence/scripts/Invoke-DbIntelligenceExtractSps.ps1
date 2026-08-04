#Requires -Version 5.1
<#
.SYNOPSIS
  Extract stored procedures into the evidence graph given a SQL connection string.

.DESCRIPTION
  Starts (or uses) DbIntelligence API, runs an index job with runSqlScan=true, then
  prints /api/maps/stored-procedures and the exported stored-procedure-map.json.

  Connection string resolution (first wins):
    1. -SqlConnectionString
    2. $env:DbIntelligence__SqlConnectionString
    3. -UseShowcaseLocalDefaults → ShowcaseDataService.Api/appsettings.json Database:Owned

  Code-inferred Showcase SP placeholders (enums/template) are printed for comparison
  with live SQL inventory. SqlScanner is read-only — never invents prod credentials.

.PARAMETER RepositoryPath
  Repo to attach the scan to (artifacts land under {repo}/.db-index/). Default: ShowcaseDataService.

.PARAMETER SqlConnectionString
  SQL Server connection string for read-only inventory.

.PARAMETER UseShowcaseLocalDefaults
  Infer LocalDB Owned connection from Showcase appsettings.json.

.PARAMETER Endpoint
  Owned (default) or SourceFacade when using Showcase defaults.

.PARAMETER ApiBase
  API base URL (default http://localhost:5088).

.PARAMETER SkipCodeTools
  Skip Codegraph/Graphify (faster SP-focused SQL + optional Roslyn scan).

.PARAMETER RunRepositoryScan
  Also Roslyn-scan the repo for code→SP references (default on).

.EXAMPLE
  .\Invoke-DbIntelligenceExtractSps.ps1 -UseShowcaseLocalDefaults

.EXAMPLE
  .\Invoke-DbIntelligenceExtractSps.ps1 `
    -RepositoryPath "D:\code\projects\my-monolith" `
    -SqlConnectionString "Server=.;Database=Monolith;Trusted_Connection=True;TrustServerCertificate=True"
#>
[CmdletBinding()]
param(
    [Alias("Path", "ProjectPath", "RepoPath")]
    [string]$RepositoryPath = "",

    [string]$SqlConnectionString = "",

    [switch]$UseShowcaseLocalDefaults,

    [ValidateSet("Owned", "SourceFacade")]
    [string]$Endpoint = "Owned",

    [string]$ApiBase = "http://localhost:5088",

    [switch]$SkipCodeTools,

    [bool]$RunRepositoryScan = $true,

    [int]$Port = 5088
)

$ErrorActionPreference = "Stop"
$Scripts = $PSScriptRoot
. (Join-Path $Scripts "Resolve-DbIntelligenceSqlConnection.ps1")

$cs = Resolve-DbIntelligenceSqlConnection `
    -SqlConnectionString $SqlConnectionString `
    -UseShowcaseLocalDefaults:$UseShowcaseLocalDefaults `
    -Endpoint $Endpoint

if ([string]::IsNullOrWhiteSpace($cs)) {
    throw @"
No SqlConnectionString resolved.
Provide -SqlConnectionString, set `$env:DbIntelligence__SqlConnectionString, or pass -UseShowcaseLocalDefaults
(reads Showcase LocalDB from ShowcaseDataService.Api/appsettings.json — database must already exist).
"@
}

if ([string]::IsNullOrWhiteSpace($RepositoryPath)) {
    $RepositoryPath = Join-Path $Scripts "..\..\DataServices\ShowcaseDataService"
}

$full = (Resolve-Path $RepositoryPath).Path

Write-Host "=== Code-inferred Showcase SP placeholders ===" -ForegroundColor Cyan
$placeholders = Get-ShowcaseProcedurePlaceholders
$placeholders | ConvertTo-Json -Depth 5 | Write-Host
Write-Host ""
Write-Host "Resolved connection endpoint: $Endpoint" -ForegroundColor Cyan
# Redact password-like segments for console
$safeCs = [regex]::Replace($cs, '(?i)(Password|Pwd)=[^;]*', '$1=***')
Write-Host "SqlConnectionString (redacted): $safeCs"

Write-Host "Checking API health at $ApiBase/api/health ..." -ForegroundColor Cyan
$health = $null
try {
    $health = Invoke-RestMethod -Uri "$ApiBase/api/health" -Method Get
}
catch {
    Write-Host "API not reachable — starting via Start-DbIntelligence.ps1 ..." -ForegroundColor Yellow
    $startScript = Join-Path $Scripts "Start-DbIntelligence.ps1"
    $argList = @(
        "-NoProfile", "-ExecutionPolicy", "Bypass",
        "-File", $startScript, "-Force", "-Port", "$Port", "-RepositoryPath", $full
    )
    Start-Process -FilePath "powershell.exe" -ArgumentList $argList -WindowStyle Minimized | Out-Null
    $deadline = (Get-Date).AddSeconds(180)
    do {
        Start-Sleep -Seconds 3
        try { $health = Invoke-RestMethod -Uri "$ApiBase/api/health" -Method Get } catch { $health = $null }
    } while ($null -eq $health -and (Get-Date) -lt $deadline)
}

if ($null -eq $health) {
    throw "API did not become healthy at $ApiBase."
}
if (-not $health.healthy) {
    Write-Warning $health.message
    throw "Prerequisites unhealthy. Run .\Install-DbIntelligencePrereqs.ps1"
}

$runCode = -not [bool]$SkipCodeTools
$bodyObj = @{
    targetRepositoryPath = $full
    runCodegraph         = $runCode
    runGraphify          = $runCode
    runRepositoryScan    = [bool]$RunRepositoryScan
    runSqlScan           = $true
    sqlConnectionString  = $cs
}

$body = $bodyObj | ConvertTo-Json
Write-Host "Starting SP extract index for $full (runSqlScan=true) ..." -ForegroundColor Cyan
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

$sp = Invoke-RestMethod -Uri "$ApiBase/api/maps/stored-procedures" -Method Get
Write-Host ""
Write-Host "=== Live stored-procedure map ($($sp.procedures.Count) entries) ===" -ForegroundColor Green
$sp.procedures | ForEach-Object {
    $name = if ($_.nameTemplate) { $_.nameTemplate } else { "$($_.schema).$($_.name)" }
    $resolved = if ($_.resolvedNames) { ($_.resolvedNames -join ", ") } else { "" }
    Write-Host ("- {0}  resolved=[{1}]  callers={2}" -f $name, $resolved, (($_.callers) -join ", "))
}

$artifact = Join-Path $full ".db-index\stored-procedure-map.json"
if (Test-Path -LiteralPath $artifact) {
    Write-Host ""
    Write-Host "Artifact: $artifact" -ForegroundColor Green
}

Write-Host ""
Write-Host "Compare live SQL names with code-inferred ResolvedNames above." -ForegroundColor Yellow
Write-Host "Missing DB objects are expected until LocalDB ShowcaseOwned/ShowcaseSource are published."
exit 0
