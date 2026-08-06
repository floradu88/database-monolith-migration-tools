#Requires -Version 5.1
<#
.SYNOPSIS
  Restore, build, and test the DbIntelligence .NET solution (and optionally Angular).

.PARAMETER Configuration
  Build configuration (default Release).

.PARAMETER SkipWeb
  Skip npm ci/install for DbIntelligence.Web.

.PARAMETER SkipTests
  Skip dotnet test.

.EXAMPLE
  .\Build-DbIntelligence.ps1
  .\Build-DbIntelligence.ps1 -SkipWeb
#>
[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$SkipWeb,
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Sln = Join-Path $Root "..\DatabaseModernization.sln"
$Web = Join-Path $Root "DbIntelligence.Web"
$Tests = Join-Path $Root "DbIntelligence.Tests\DbIntelligence.Tests.csproj"
$NodeInit = Join-Path $PSScriptRoot "Initialize-DbIntelligenceNode.ps1"

if (-not (Test-Path $Sln)) {
    throw "Solution not found: $Sln"
}

Write-Host "Restoring $Sln ..." -ForegroundColor Cyan
dotnet restore $Sln
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Release API builds copy into Api\bin; stop a running instance so DLLs are not locked.
$apiListeners = @()
try { $apiListeners = @(Get-NetTCPConnection -LocalPort 5088 -State Listen -ErrorAction SilentlyContinue) } catch { }
foreach ($listener in $apiListeners) {
    Write-Warning "Stopping process $($listener.OwningProcess) locking port 5088 before build"
    Stop-Process -Id $listener.OwningProcess -Force -ErrorAction SilentlyContinue
}
Get-Process -Name "DbIntelligence.Api" -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Warning "Stopping DbIntelligence.Api PID $($_.Id)"
    Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
}
if ($apiListeners.Count -gt 0) { Start-Sleep -Seconds 1 }

Write-Host "Building ($Configuration) ..." -ForegroundColor Cyan
dotnet build $Sln -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (-not $SkipTests) {
    Write-Host "Testing ..." -ForegroundColor Cyan
    dotnet test $Tests -c $Configuration --no-build --verbosity minimal
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

if (-not $SkipWeb) {
    . $NodeInit -Quiet
    if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
        Write-Warning "npm not found; attempting user-scoped fnm Node install..."
        & $NodeInit -Install -Yes
        . $NodeInit -Quiet
    }

    if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
        Write-Warning "npm not found; skipping Angular restore. Run .\Initialize-DbIntelligenceNode.ps1 -Install -Yes or pass -SkipWeb."
    }
    else {
        $npmCmd = if (Get-Command npm.cmd -ErrorAction SilentlyContinue) { "npm.cmd" } else { "npm" }
        if (Get-Variable -Name PSNativeCommandUseErrorActionPreference -ErrorAction SilentlyContinue) {
            $PSNativeCommandUseErrorActionPreference = $false
        }

        Write-Host "Installing Angular dependencies ..." -ForegroundColor Cyan
        Write-Host "Using $npmCmd : $((Get-Command $npmCmd).Source)" -ForegroundColor DarkGray
        Push-Location $Web
        try {
            if (Test-Path "package-lock.json") {
                & $npmCmd ci
                if ($LASTEXITCODE -ne 0) {
                    Write-Warning "npm ci failed; falling back to npm install"
                    & $npmCmd install
                }
            }
            else {
                & $npmCmd install
            }
            if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

            Write-Host "Building Angular (development) ..." -ForegroundColor Cyan
            & $npmCmd run build -- --configuration=development
            if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        }
        finally {
            Pop-Location
        }
    }
}

Write-Host "Build pipeline succeeded." -ForegroundColor Green
exit 0
