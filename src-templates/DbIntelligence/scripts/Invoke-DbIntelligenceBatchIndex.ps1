#Requires -Version 5.1
<#
.SYNOPSIS
  Batch-index every child folder under a parent path (each child = one project).
  Results are written to each project's root by default.

.PARAMETER ParentFolderPath
  Folder that contains one project per subfolder.

.PARAMETER RequireProjectMarkers
  Only include folders with .git / *.sln / *.csproj / package.json / etc.

.PARAMETER ApiBase
  API base URL (default http://localhost:5088).

.EXAMPLE
  .\Invoke-DbIntelligenceBatchIndex.ps1 -ParentFolderPath "D:\code\projects"
  .\Invoke-DbIntelligenceBatchIndex.ps1 -ParentFolderPath "C:\code"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ParentFolderPath,
    [string]$ApiBase = "http://localhost:5088",
    [switch]$RequireProjectMarkers,
    [switch]$RefreshGraphify,
    [string]$ArtifactsRelativeDirectory = ".db-index"
)

$ErrorActionPreference = "Stop"
$full = (Resolve-Path $ParentFolderPath).Path

Write-Host "Discovering projects under $full ..." -ForegroundColor Cyan
$discoverUri = "$ApiBase/api/index/discover?parentFolderPath=$([uri]::EscapeDataString($full))"
if ($RequireProjectMarkers) { $discoverUri += "&requireProjectMarkers=true" }
$discovered = Invoke-RestMethod -Uri $discoverUri -Method Get
Write-Host "Found $($discovered.projects.Count) project(s):"
$discovered.projects | ForEach-Object {
    $marker = if ($_.hasProjectMarker) { "marker" } else { "no-marker" }
    Write-Host "  - $($_.name) [$marker]"
}

if ($discovered.projects.Count -eq 0) {
    throw "No projects discovered."
}

$body = @{
    parentFolderPath           = $full
    runCodegraph               = $true
    runGraphify                = $true
    refreshGraphify            = [bool]$RefreshGraphify
    runRepositoryScan          = $true
    runSqlScan                 = $false
    requireProjectMarkers      = [bool]$RequireProjectMarkers
    continueOnError            = $true
    artifactsRelativeDirectory = $ArtifactsRelativeDirectory
} | ConvertTo-Json

Write-Host "Starting batch index ..." -ForegroundColor Cyan
$job = Invoke-RestMethod -Uri "$ApiBase/api/index/batch" -Method Post -Body $body -ContentType "application/json"
Write-Host "Batch id: $($job.id)"

do {
    Start-Sleep -Seconds 3
    $status = Invoke-RestMethod -Uri "$ApiBase/api/index/batch/$($job.id)" -Method Get
    $line = "[$($status.status)] $($status.completedProjects)/$($status.totalProjects) current=$($status.currentProject) $($status.message)"
    Write-Host $line
} while ($status.status -eq "Pending" -or $status.status -eq "Running")

Write-Host ""
Write-Host "=== Per-project results ===" -ForegroundColor Yellow
$status.projects | ForEach-Object {
    Write-Host ("{0,-24} {1,-10} nodes={2} edges={3} artifacts={4}" -f $_.name, $_.status, $_.nodeCount, $_.edgeCount, $_.artifactsDirectory)
    Write-Host "  $($_.message)"
}

Write-Host ""
Write-Host ($status | ConvertTo-Json -Depth 8)
$summary = Join-Path $full "db-intelligence-batch-summary.json"
if (Test-Path $summary) {
    Write-Host "Parent summary: $summary" -ForegroundColor Green
}

if ($status.status -ne "Completed") {
    throw "Batch finished with status $($status.status)"
}
exit 0
