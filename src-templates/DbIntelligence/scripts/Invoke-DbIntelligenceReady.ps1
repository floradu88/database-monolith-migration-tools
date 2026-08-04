#Requires -Version 5.1
<#
.SYNOPSIS
  One command: install/check tools (no admin), build, health-check, start API, index a project.

.DESCRIPTION
  Only the project path is required. Everything else is auto-confirmed, checked, and run:
    1. User-scoped fnm Node + Codegraph (fnm exec --using=lts-latest)
    2. Python / pip / graphify / codegraph via CLI --install-preqs
    3. Restore + build + test
    4. CLI health must be healthy
    5. Start API on :5088 if needed
    6. Index the given repository path (Codegraph + Graphify + repo scan; optional SQL SP inventory)
    7. Print map counts and exit non-zero on failure

.PARAMETER RepositoryPath
  Folder to index (only required argument). Aliases: Path, ProjectPath, RepoPath.

.PARAMETER Port
  API port (default 5088).

.PARAMETER ApiBase
  Override API base URL (default http://localhost:<Port>).

.PARAMETER SkipBuild
  Skip restore/build/test (use when already built).

.PARAMETER SkipPrereqs
  Skip prerequisite install (still runs health check).

.PARAMETER WithWeb
  Also restore Angular npm dependencies during build.

.PARAMETER SkipTests
  Skip dotnet test during build.

.PARAMETER SqlConnectionString
  When set, enables read-only SQL inventory (runSqlScan) so live SPs are extracted into the map.

.PARAMETER UseShowcaseLocalDefaults
  Infer LocalDB Owned connection from Showcase appsettings.json (non-secret kit placeholders).

.EXAMPLE
  .\Invoke-DbIntelligenceReady.ps1 "D:\code\projects\my-app"
  .\Invoke-DbIntelligenceReady.ps1 -RepositoryPath "C:\git\my-app"
  .\Invoke-DbIntelligenceReady.ps1 ".\ShowcaseDataService" -UseShowcaseLocalDefaults
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [Alias("Path", "ProjectPath", "RepoPath")]
    [string]$RepositoryPath,

    [int]$Port = 5088,
    [string]$ApiBase = "",

    [string]$SqlConnectionString = "",
    [switch]$UseShowcaseLocalDefaults,

    [switch]$SkipBuild,
    [switch]$SkipPrereqs,
    [switch]$WithWeb,
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
$Scripts = $PSScriptRoot
$Root = Split-Path -Parent $Scripts
$NodeInit = Join-Path $Scripts "Initialize-DbIntelligenceNode.ps1"

if (-not $ApiBase) {
    $ApiBase = "http://localhost:$Port"
}

function Write-Step([string]$Message) {
    Write-Host "`n=== $Message ===" -ForegroundColor Cyan
}

function Test-ListeningOnPort([int]$PortNumber) {
    try {
        return @(Get-NetTCPConnection -LocalPort $PortNumber -State Listen -ErrorAction SilentlyContinue).Count -gt 0
    }
    catch {
        return $false
    }
}

function Get-ApiHealth([string]$Base, [int]$TimeoutSec = 5) {
    try {
        return Invoke-RestMethod -Uri "$Base/api/health" -Method Get -TimeoutSec $TimeoutSec
    }
    catch {
        return $null
    }
}

function Wait-ApiReady([string]$Base, [int]$TimeoutSec = 180) {
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    $lastErr = $null
    while ((Get-Date) -lt $deadline) {
        try {
            $health = Invoke-RestMethod -Uri "$Base/api/health" -Method Get -TimeoutSec 5
            if ($null -ne $health) {
                return $health
            }
        }
        catch {
            $lastErr = $_.Exception.Message
        }
        Start-Sleep -Seconds 2
    }
    throw "API did not become ready at $Base within ${TimeoutSec}s. Last error: $lastErr"
}

# --- resolve project path ---
if (-not (Test-Path -LiteralPath $RepositoryPath)) {
    throw "RepositoryPath not found: $RepositoryPath"
}
$full = (Resolve-Path -LiteralPath $RepositoryPath).Path

Write-Host "DbIntelligence ready (one-shot)" -ForegroundColor Cyan
Write-Host "  Kit:     $Root"
Write-Host "  Project: $full"
Write-Host "  API:     $ApiBase"
Write-Host "  Mode:    no-admin fnm Node + auto-install + health + index"

# --- 1) prereqs (fnm/node/codegraph + python/graphify) ---
if (-not $SkipPrereqs) {
    Write-Step "Prerequisites (auto-yes, no admin)"
    & (Join-Path $Scripts "Install-DbIntelligencePrereqs.ps1") -Yes
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Prerequisite installer exited $LASTEXITCODE; continuing to health check."
    }
}

# Activate fnm Node/npm/codegraph on PATH for this session.
if (Test-Path $NodeInit) {
    . $NodeInit -Quiet
}

# --- 2) build ---
if (-not $SkipBuild) {
    Write-Step "Build & test"
    if ($WithWeb -and $SkipTests) {
        & (Join-Path $Scripts "Build-DbIntelligence.ps1") -SkipTests
    }
    elseif ($WithWeb) {
        & (Join-Path $Scripts "Build-DbIntelligence.ps1")
    }
    elseif ($SkipTests) {
        & (Join-Path $Scripts "Build-DbIntelligence.ps1") -SkipWeb -SkipTests
    }
    else {
        & (Join-Path $Scripts "Build-DbIntelligence.ps1") -SkipWeb
    }
    if ($LASTEXITCODE -ne 0) {
        throw "Build/test failed with exit code $LASTEXITCODE"
    }
}

# --- 3) health (must pass) ---
Write-Step "Health check"
& (Join-Path $Scripts "Test-DbIntelligenceHealth.ps1")
if ($LASTEXITCODE -ne 0) {
    throw "Health check failed (exit $LASTEXITCODE). Fix missing tools and re-run. Prefer: .\Install-DbIntelligencePrereqs.ps1 -Yes"
}

# --- 4) ensure API ---
Write-Step "API on $ApiBase"
$existing = Get-ApiHealth $ApiBase
$startedApi = $false
$apiProc = $null

if ($null -eq $existing) {
    $startScript = Join-Path $Scripts "Start-DbIntelligence.ps1"
    $argList = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", $startScript,
        "-Force",
        "-Port", "$Port",
        "-RepositoryPath", $full
    )
    Write-Host "Starting API in a background PowerShell window..."
    $apiProc = Start-Process -FilePath "powershell.exe" -ArgumentList $argList -PassThru -WindowStyle Minimized
    $startedApi = $true
    $health = Wait-ApiReady -Base $ApiBase -TimeoutSec 180
}
else {
    Write-Host "API already reachable."
    $health = $existing
}

if (-not $health.healthy) {
    Write-Warning $health.message
    if ($health.installHint) { Write-Warning $health.installHint }
    throw "API reports unhealthy prerequisites. Re-run with prereqs enabled (default)."
}

Write-Host "API health: $($health.status)" -ForegroundColor Green

# --- 5) index ---
Write-Step "Index $full"
. (Join-Path $Scripts "Resolve-DbIntelligenceSqlConnection.ps1")
$cs = Resolve-DbIntelligenceSqlConnection `
    -SqlConnectionString $SqlConnectionString `
    -UseShowcaseLocalDefaults:$UseShowcaseLocalDefaults
$runSql = -not [string]::IsNullOrWhiteSpace($cs)
if ($runSql) {
    Write-Host "SQL scan enabled (read-only inventory)." -ForegroundColor Cyan
}

$body = @{
    targetRepositoryPath = $full
    runCodegraph         = $true
    runGraphify          = $true
    runRepositoryScan    = $true
    runSqlScan           = $runSql
    sqlConnectionString  = $(if ($runSql) { $cs } else { $null })
} | ConvertTo-Json

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

Write-Step "Done"
Write-Host "Project:              $full" -ForegroundColor Green
Write-Host "code-to-db entries:   $($map.entries.Count)" -ForegroundColor Green
Write-Host "stored procedures:    $($sp.procedures.Count)" -ForegroundColor Green
Write-Host "API:                  $ApiBase (left running)"
Write-Host "UI (optional):        .\scripts\Start-DbIntelligenceWeb.ps1  -> http://localhost:4200"
if ($startedApi -and $apiProc) {
    Write-Host "API process:          PID $($apiProc.Id) (minimized PowerShell)"
}

exit 0
