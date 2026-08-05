#Requires -Version 5.1
<#
.SYNOPSIS
  Restore, build, and test CodegraphChat; optionally build Angular and publish to Api/wwwroot.

.PARAMETER Configuration
  Build configuration (default Release).

.PARAMETER SkipWeb
  Skip npm install / Angular build / wwwroot publish.

.PARAMETER SkipTests
  Skip dotnet test.

.PARAMETER Yes
  Auto-confirm Node/fnm install prompts when npm is missing.

.EXAMPLE
  .\Build-CodegraphChat.ps1
  .\Build-CodegraphChat.ps1 -SkipWeb
  .\Build-CodegraphChat.ps1 -Yes
#>
[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$SkipWeb,
    [switch]$SkipTests,
    [switch]$Yes
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Sln = Join-Path $Root "CodegraphChat.sln"
$Web = Join-Path $Root "CodegraphChat.Web"
$ApiWwwroot = Join-Path $Root "CodegraphChat.Api\wwwroot"
$NodeInit = Join-Path (Split-Path -Parent $Root) "DbIntelligence\scripts\Initialize-DbIntelligenceNode.ps1"

if (-not (Test-Path $Sln)) {
    throw "Solution not found: $Sln"
}

Write-Host "Restoring $Sln ..." -ForegroundColor Cyan
dotnet restore $Sln
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$apiListeners = @()
try { $apiListeners = @(Get-NetTCPConnection -LocalPort 5091 -State Listen -ErrorAction SilentlyContinue) } catch { }
foreach ($listener in $apiListeners) {
    Write-Warning "Stopping process $($listener.OwningProcess) locking port 5091 before build"
    Stop-Process -Id $listener.OwningProcess -Force -ErrorAction SilentlyContinue
}
Get-Process -Name "CodegraphChat.Api" -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Warning "Stopping CodegraphChat.Api PID $($_.Id)"
    Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
}
if ($apiListeners.Count -gt 0) { Start-Sleep -Seconds 1 }

Write-Host "Building ($Configuration) ..." -ForegroundColor Cyan
dotnet build $Sln -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (-not $SkipTests) {
    Write-Host "Testing ..." -ForegroundColor Cyan
    dotnet test $Sln -c $Configuration --no-build --verbosity minimal
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

if (-not $SkipWeb) {
    if (-not (Test-Path $NodeInit)) {
        throw "Missing DbIntelligence Node helper: $NodeInit"
    }

    . $NodeInit -Quiet
    if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
        Write-Warning "npm not found; installing user-scoped fnm Node (no admin)..."
        if ($Yes) {
            & $NodeInit -Install -Yes
        }
        else {
            & $NodeInit -Install
        }
        . $NodeInit -Quiet
    }

    function Invoke-KitNpm {
        param([Parameter(ValueFromRemainingArguments = $true)][string[]]$NpmArgs)
        if (Get-Command fnm -ErrorAction SilentlyContinue) {
            Write-Host "fnm exec --using=lts-latest -- npm $($NpmArgs -join ' ')" -ForegroundColor DarkGray
            & fnm exec --using=lts-latest -- npm @NpmArgs
            return $LASTEXITCODE
        }
        Write-Host "npm $($NpmArgs -join ' ')  (fnm not on PATH; using activated npm)" -ForegroundColor DarkGray
        & npm @NpmArgs
        return $LASTEXITCODE
    }

    if (-not (Get-Command npm -ErrorAction SilentlyContinue) -and -not (Get-Command fnm -ErrorAction SilentlyContinue)) {
        Write-Warning "npm/fnm not found; skipping Angular build. Run ..\DbIntelligence\scripts\Initialize-DbIntelligenceNode.ps1 -Install -Yes or pass -SkipWeb."
    }
    else {
        Write-Host "Installing Angular dependencies (prefer fnm exec --using=lts-latest)..." -ForegroundColor Cyan
        Push-Location $Web
        try {
            if (Test-Path "package-lock.json") {
                $code = Invoke-KitNpm @("ci")
                if ($code -ne 0) {
                    Write-Warning "npm ci failed; falling back to npm install"
                    $code = Invoke-KitNpm @("install")
                }
            }
            else {
                $code = Invoke-KitNpm @("install")
            }
            if ($code -ne 0) { exit $code }

            Write-Host "Building Angular (production) ..." -ForegroundColor Cyan
            $code = Invoke-KitNpm @("exec", "--", "ng", "build", "--configuration", "production")
            if ($code -ne 0) {
                # Fallback when npm exec path is awkward under fnm
                if (Get-Command fnm -ErrorAction SilentlyContinue) {
                    & fnm exec --using=lts-latest -- npx ng build --configuration production
                    $code = $LASTEXITCODE
                }
                else {
                    & npx ng build --configuration production
                    $code = $LASTEXITCODE
                }
            }
            if ($code -ne 0) { exit $code }

            $distRoot = Join-Path $Web "dist\codegraph-chat.web"
            $browser = Join-Path $distRoot "browser"
            $source = if (Test-Path (Join-Path $browser "index.html")) { $browser } elseif (Test-Path (Join-Path $distRoot "index.html")) { $distRoot } else { $null }
            if (-not $source) {
                throw "Angular build output not found under $distRoot (expected index.html in browser/ or dist root)."
            }

            Write-Host "Publishing SPA to $ApiWwwroot ..." -ForegroundColor Cyan
            New-Item -ItemType Directory -Force -Path $ApiWwwroot | Out-Null
            Get-ChildItem -LiteralPath $ApiWwwroot -Force | Where-Object { $_.Name -ne ".gitkeep" } | Remove-Item -Recurse -Force
            Copy-Item -Path (Join-Path $source "*") -Destination $ApiWwwroot -Recurse -Force
            if (-not (Test-Path (Join-Path $ApiWwwroot "index.html"))) {
                throw "Publish failed: $ApiWwwroot\index.html missing."
            }
            Write-Host "SPA published (single-host UI: http://localhost:5091/)." -ForegroundColor Green
        }
        finally {
            Pop-Location
        }
    }
}

Write-Host "Build pipeline succeeded." -ForegroundColor Green
exit 0
