#Requires -Version 5.1
<#
.SYNOPSIS
  One command: set up local Python venv (no admin) and map YAML dependencies to Mermaid Markdown.

.DESCRIPTION
  Only the folder (or single YAML file) path is required. Everything else is auto-confirmed:
    1. Ensure local .venv next to this tool and install PyYAML
    2. Recursively scan *.yaml / *.yml with schema-aware dependency adapters
    3. Write topology Markdown (Mermaid + Dependencies + per-file explanations)
    4. Write one *.explain.md per YAML file under a sibling explains folder

.PARAMETER RepositoryPath
  Folder to scan recursively, or a single .yaml/.yml file. Aliases: Path, ProjectPath, RepoPath, Repo.

.PARAMETER Output
  Optional output .md path. Default: {folder}\topology.md (or next to a single file).

.PARAMETER ExplainDir
  Optional folder for one explain Markdown per YAML file.
  Default: {output-directory}\topology-explains

.PARAMETER Direction
  Mermaid flowchart direction: LR, RL, TB, BT (default TB).

.PARAMETER Title
  Markdown title. Default includes the folder/file name.

.PARAMETER Adapters
  Optional comma-separated adapter list.

.PARAMETER NoStubs
  Do not create stub nodes for unresolved dependency targets.

.PARAMETER NoExplanations
  Omit per-file explanations from the main Markdown (still writes ExplainDir unless empty and skipped).

.PARAMETER SkipExplainFiles
  Do not write the per-file *.explain.md directory.

.PARAMETER Open
  Open the generated Markdown after success (default Windows association).

.EXAMPLE
  .\Invoke-YamlTopologyReady.ps1 "D:\path\to\yaml-repo"

.EXAMPLE
  .\Invoke-YamlTopologyReady.ps1 "D:\path\to\compose\docker-compose.yml" -Open
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [Alias("Path", "ProjectPath", "RepoPath", "Repo")]
    [string]$RepositoryPath,

    [string]$Output = "",

    [string]$ExplainDir = "",

    [ValidateSet("LR", "RL", "TB", "BT")]
    [string]$Direction = "TB",

    [string]$Title = "",

    [string]$Adapters = "",

    [switch]$NoStubs,

    [switch]$NoExplanations,

    [switch]$SkipExplainFiles,

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
$isFile = -not (Test-Path -LiteralPath $RepoFull -PathType Container)
$leafName = Split-Path -Leaf $RepoFull
if (-not $Title) {
    $Title = "YAML Topology - $leafName"
}

if (-not $Output) {
    if ($isFile) {
        $Output = Join-Path (Split-Path -Parent $RepoFull) "topology.md"
    } else {
        $Output = Join-Path $RepoFull "topology.md"
    }
} elseif (-not [System.IO.Path]::IsPathRooted($Output)) {
    $Output = Join-Path (Get-Location).Path $Output
}

$outputParent = Split-Path -Parent $Output
if (-not $outputParent) {
    $outputParent = (Get-Location).Path
}
if (-not $ExplainDir -and -not $SkipExplainFiles) {
    $ExplainDir = Join-Path $outputParent "topology-explains"
} elseif ($ExplainDir -and -not [System.IO.Path]::IsPathRooted($ExplainDir)) {
    $ExplainDir = Join-Path (Get-Location).Path $ExplainDir
}
if ($SkipExplainFiles) {
    $ExplainDir = ""
}

Write-Host "=== YAML Topology Ready (one command) ===" -ForegroundColor Cyan
Write-Host "Repository: $RepoFull"
Write-Host "Output:     $Output"
if ($ExplainDir) {
    Write-Host "Explains:   $ExplainDir"
}

$runArgs = @{
    Repo      = $RepoFull
    Output    = $Output
    Direction = $Direction
    Title     = $Title
}
if ($Adapters) { $runArgs.Adapters = $Adapters }
if ($NoStubs) { $runArgs.NoStubs = $true }
if ($NoExplanations) { $runArgs.NoExplanations = $true }
if ($ExplainDir) { $runArgs.ExplainDir = $ExplainDir }

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
if ($ExplainDir -and (Test-Path -LiteralPath $ExplainDir)) {
    $explainCount = @(Get-ChildItem -LiteralPath $ExplainDir -Filter "*.explain.md" -File -ErrorAction SilentlyContinue).Count
    Write-Host "Per-file explanations ($explainCount):" -ForegroundColor Green
    Write-Host "  $((Resolve-Path -LiteralPath $ExplainDir).Path)"
}
Write-Host "Open in GitHub or a Mermaid-capable Markdown preview to view the diagram."

if ($Open) {
    Invoke-Item -LiteralPath $resolvedOut
}
