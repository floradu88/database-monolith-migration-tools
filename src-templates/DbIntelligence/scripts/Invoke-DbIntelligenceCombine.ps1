#Requires -Version 5.1
<#
.SYNOPSIS
  Load every child project's exported graph.json under a parent folder and present them as ONE live graph.

.DESCRIPTION
  Calls POST /api/graphs/combine. Prefer after batch index (or whenever per-project JSON exists).
  Code nodes are project-prefixed; DB nodes are shared across projects by default.
  Also writes {parent}\.db-index-combined\ (graph.json, maps, GRAPH_REPORT.md, findings.html) unless -SkipExport.

.PARAMETER ParentFolderPath
  Folder that contains one project per subfolder (each with .db-index\graph.json).

.PARAMETER ApiBase
  API base URL (default http://localhost:5088).

.EXAMPLE
  .\Invoke-DbIntelligenceCombine.ps1 -ParentFolderPath "D:\code\projects"
  .\Invoke-DbIntelligenceCombine.ps1 -ParentFolderPath "C:\code" -RequireProjectMarkers
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [Alias("Path", "ProjectPath")]
    [string]$ParentFolderPath,

    [string]$ApiBase = "http://localhost:5088",
    [switch]$RequireProjectMarkers,
    [switch]$SkipExport,
    [string]$ArtifactsRelativeDirectory = ".db-index"
)

$ErrorActionPreference = "Stop"
$full = (Resolve-Path -LiteralPath $ParentFolderPath).Path

Write-Host "Checking API at $ApiBase/api/health ..." -ForegroundColor Cyan
try {
    $null = Invoke-RestMethod -Uri "$ApiBase/api/health" -Method Get
}
catch {
    throw "API is not reachable at $ApiBase. Start it with .\Start-DbIntelligence.ps1 -Force first. $_"
}

$body = @{
    parentFolderPath           = $full
    artifactsRelativeDirectory = $ArtifactsRelativeDirectory
    requireProjectMarkers      = [bool]$RequireProjectMarkers
    shareDatabaseNodes         = $true
    onlyCompletedFromSummary   = $true
    exportCombined             = -not [bool]$SkipExport
} | ConvertTo-Json

Write-Host "Combining project graphs under $full ..." -ForegroundColor Cyan
$result = Invoke-RestMethod -Uri "$ApiBase/api/graphs/combine" -Method Post -Body $body -ContentType "application/json"

Write-Host "Loaded:  $($result.projectsLoaded)" -ForegroundColor Green
Write-Host "Skipped: $($result.projectsSkipped)"
Write-Host "Nodes:   $($result.nodeCount)" -ForegroundColor Green
Write-Host "Edges:   $($result.edgeCount)" -ForegroundColor Green
if ($result.combinedOutputDirectory) {
    Write-Host "Export:  $($result.combinedOutputDirectory)" -ForegroundColor Green
}
Write-Host "Open UI: .\scripts\Start-DbIntelligenceWeb.ps1  -> http://localhost:4200"
exit 0
