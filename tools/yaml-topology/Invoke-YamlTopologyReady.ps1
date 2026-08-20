#Requires -Version 5.1
<#
.SYNOPSIS
  One command: set up local Python venv (no admin) and map YAML dependencies to Mermaid Markdown.

.DESCRIPTION
  Only the folder path is required. Everything else is auto-confirmed:
    1. Ensure local .venv next to this tool and install PyYAML
    2. Recursively scan *.yaml / *.yml with schema-aware dependency adapters
    3. Write topology Markdown (Mermaid + Dependencies table) under the scanned folder

.PARAMETER RepositoryPath
  Folder to scan recursively. Aliases: Path, ProjectPath, RepoPath, Repo.

.PARAMETER Output
  Optional output .md path. Default: {RepositoryPath}\topology.md

.PARAMETER Direction
  Mermaid flowchart direction: LR, RL, TB, BT (default TB).

.PARAMETER Title
  Markdown title. Default includes the folder name.

.PARAMETER Adapters
  Optional comma-separated adapter list (compose,kubernetes,github-actions,azure-devops,cloudformation,kit-manifest,generic).

.PARAMETER NoStubs
  Do not create stub nodes for unresolved dependency targets.

.PARAMETER Open
  Open the generated Markdown after success (default Windows association).

.EXAMPLE
  .\Invoke-YamlTopologyReady.ps1 "D:\path\to\yaml-repo"

.EXAMPLE
  .\Invoke-YamlTopologyReady.ps1 "D:\path\to\yaml-repo" -Direction LR -Open
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [Alias("Path", "ProjectPath", "RepoPath", "Repo")]
    [string]$RepositoryPath,

    [string]$Output = "",

    [ValidateSet("LR", "RL", "TB", "BT")]
    [string]$Direction = "TB",

    [string]$Title = "",

    [string]$Adapters = "",

    [switch]$NoStubs,

    [switch]$Open
)

$ErrorActionPreference = "Stop"
$Here = Split-Path -Parent $MyInvocation.MyCommand.Path
$Runner = Join-Path $Here "run-topology.ps1"

if (-not (Test-Path -LiteralPath $RepositoryPath)) {
    throw "Repository path not found: $RepositoryPath"
}
if (-not (Test-Path -LiteralPath $Runner)) {
    throw "Missing runner: $Runner"
}

$RepoFull = (Resolve-Path -LiteralPath $RepositoryPath).Path
$folderName = Split-Path -Leaf $RepoFull
if (-not $Title) {
    $Title = "YAML Topology - $folderName"
}
if (-not $Output) {
    $Output = Join-Path $RepoFull "topology.md"
} elseif (-not [System.IO.Path]::IsPathRooted($Output)) {
    $Output = Join-Path (Get-Location).Path $Output
}

Write-Host "=== YAML Topology Ready (one command) ===" -ForegroundColor Cyan
Write-Host "Repository: $RepoFull"
Write-Host "Output:     $Output"

$runArgs = @{
    Repo      = $RepoFull
    Output    = $Output
    Direction = $Direction
    Title     = $Title
}
if ($Adapters) { $runArgs.Adapters = $Adapters }
if ($NoStubs) { $runArgs.NoStubs = $true }

& $Runner @runArgs
if ($LASTEXITCODE -ne 0 -and $null -ne $LASTEXITCODE) {
    exit $LASTEXITCODE
}

if (-not (Test-Path -LiteralPath $Output)) {
    throw "Expected output was not created: $Output"
}

$resolvedOut = (Resolve-Path -LiteralPath $Output).Path
Write-Host ""
Write-Host "Ready. Topology written to:" -ForegroundColor Green
Write-Host "  $resolvedOut"
Write-Host "Open in GitHub or a Mermaid-capable Markdown preview to view the diagram."

if ($Open) {
    Invoke-Item -LiteralPath $resolvedOut
}
