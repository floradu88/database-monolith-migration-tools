#Requires -Version 5.1
<#
.SYNOPSIS
  Create Showcase LocalDB databases and publish schemas, EF Items table, and SPs (lab only).

.DESCRIPTION
  End-to-end lab setup for SP extract/export:
    1. Start LocalDB + CREATE ShowcaseOwned / ShowcaseSource
    2. PreDeploy (schemas) on both
    3. EF migrate showcase.Items on Owned
    4. Apply Contract + Programmability (+ PostDeploy) via sqlcmd (default) or optional SqlPackage
    5. Mirror SQL-project objects onto ShowcaseSource for SourceFacade lab demos
    6. Optionally export SPs via Export-DatabaseStoredProcedures.ps1 and assert expected names

  Never invents production credentials. Never runs Cutover destructive waves.

.PARAMETER SkipEf
  Skip EF database update (schemas/SPs only).

.PARAMETER SkipExport
  Skip calling Export-DatabaseStoredProcedures.ps1 at the end.

.PARAMETER UseSqlPackage
  Prefer SqlPackage dacpac publish when sqlpackage.exe is on PATH (still falls back to sqlcmd).

.PARAMETER ExportDirectory
  Folder for exported .sql / list files (default: %TEMP%\showcase-sp-export).

.EXAMPLE
  .\Initialize-ShowcaseLocalDb.ps1
  .\Initialize-ShowcaseLocalDb.ps1 -SkipExport
#>
[CmdletBinding()]
param(
    [switch]$SkipEf,
    [switch]$SkipExport,
    [switch]$UseSqlPackage,
    [string]$ExportDirectory = ""
)

$ErrorActionPreference = "Stop"
$ShowcaseRoot = Split-Path -Parent $PSScriptRoot
$DatabaseRoot = Join-Path $ShowcaseRoot "ShowcaseDataService.Database"
$MigrationsProj = Join-Path $ShowcaseRoot "ShowcaseDataService.Migrations\ShowcaseDataService.Migrations.csproj"
$ApiProj = Join-Path $ShowcaseRoot "ShowcaseDataService.Api\ShowcaseDataService.Api.csproj"
$DbIntelScripts = Join-Path $ShowcaseRoot "..\..\DbIntelligence\scripts"
$Server = "(localdb)\mssqllocaldb"
$OwnedDb = "ShowcaseOwned"
$SourceDb = "ShowcaseSource"

function Write-Step([string]$Message) {
    Write-Host "`n=== $Message ===" -ForegroundColor Cyan
}

function Invoke-SqlCmdBatch {
    param(
        [Parameter(Mandatory = $true)][string]$Database,
        [Parameter(Mandatory = $true)][string]$InputFile
    )
    if (-not (Test-Path -LiteralPath $InputFile)) {
        throw "SQL file not found: $InputFile"
    }
    Write-Host "sqlcmd -d $Database -i $InputFile"
    & sqlcmd -S $Server -E -d $Database -b -I -i $InputFile
    if ($LASTEXITCODE -ne 0) {
        throw "sqlcmd failed ($LASTEXITCODE) for $InputFile on $Database"
    }
}

function Invoke-SqlCmdQuery {
    param(
        [Parameter(Mandatory = $true)][string]$Query,
        [string]$Database = "master"
    )
    & sqlcmd -S $Server -E -d $Database -b -Q $Query
    if ($LASTEXITCODE -ne 0) {
        throw "sqlcmd query failed ($LASTEXITCODE)"
    }
}

function Publish-SqlProjectObjects {
    param([Parameter(Mandatory = $true)][string]$Database)

    $contract = Join-Path $DatabaseRoot "Contract\DatabaseContract.sql"
    $post = Join-Path $DatabaseRoot "Scripts\PostDeploy.sql"
    $progDir = Join-Path $DatabaseRoot "Programmability"

    if ($UseSqlPackage) {
        $sqlpackage = Get-Command sqlpackage -ErrorAction SilentlyContinue
        $dacpac = Join-Path $DatabaseRoot "bin\Debug\ShowcaseDataService.Database.dacpac"
        if ($sqlpackage -and (Test-Path -LiteralPath $dacpac)) {
            Write-Host "SqlPackage publish -> $Database"
            & sqlpackage /Action:Publish /SourceFile:$dacpac `
                /TargetConnectionString:"Server=$Server;Database=$Database;Trusted_Connection=True;TrustServerCertificate=True"
            if ($LASTEXITCODE -eq 0) { return }
            Write-Warning "SqlPackage failed; falling back to sqlcmd."
        }
        else {
            Write-Warning "SqlPackage/dacpac unavailable; using sqlcmd path."
        }
    }

    Invoke-SqlCmdBatch -Database $Database -InputFile $contract
    Get-ChildItem -LiteralPath $progDir -Filter *.sql -File |
        Sort-Object Name |
        ForEach-Object { Invoke-SqlCmdBatch -Database $Database -InputFile $_.FullName }
    Invoke-SqlCmdBatch -Database $Database -InputFile $post
}

# --- prerequisites ---
Write-Step "Prerequisites"
foreach ($cmd in @("sqllocaldb", "sqlcmd", "dotnet")) {
    if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) {
        throw "Required tool '$cmd' not found on PATH."
    }
}

Write-Step "Start LocalDB"
& sqllocaldb start mssqllocaldb | Out-Host

Write-Step "Create databases"
Invoke-SqlCmdQuery -Query @"
IF DB_ID(N'$OwnedDb') IS NULL CREATE DATABASE [$OwnedDb];
IF DB_ID(N'$SourceDb') IS NULL CREATE DATABASE [$SourceDb];
"@

$preDeploy = Join-Path $DatabaseRoot "Scripts\PreDeploy.sql"
Write-Step "PreDeploy schemas"
Invoke-SqlCmdBatch -Database $OwnedDb -InputFile $preDeploy
Invoke-SqlCmdBatch -Database $SourceDb -InputFile $preDeploy

if (-not $SkipEf) {
    Write-Step "EF migrate showcase.Items -> $OwnedDb"
    if (-not (Test-Path -LiteralPath $MigrationsProj)) {
        throw "Migrations project missing: $MigrationsProj"
    }
    $env:Database__OwnedConnectionString =
        "Server=$Server;Database=$OwnedDb;Trusted_Connection=True;TrustServerCertificate=True"
    $env:Database__Owned__ConnectionString = $env:Database__OwnedConnectionString
    & dotnet ef database update `
        --project $MigrationsProj `
        --startup-project $ApiProj
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet ef database update failed ($LASTEXITCODE)."
    }
}

Write-Step "Publish SQL-project objects -> $OwnedDb"
Publish-SqlProjectObjects -Database $OwnedDb

Write-Step "Mirror SQL-project objects -> $SourceDb (SourceFacade lab)"
Publish-SqlProjectObjects -Database $SourceDb

# SourceFacade lab also needs Items for SP bodies that SELECT from showcase.Items
if (-not $SkipEf) {
    Write-Step "EF migrate showcase.Items -> $SourceDb (lab mirror)"
    $env:Database__OwnedConnectionString =
        "Server=$Server;Database=$SourceDb;Trusted_Connection=True;TrustServerCertificate=True"
    $env:Database__Owned__ConnectionString = $env:Database__OwnedConnectionString
    & dotnet ef database update `
        --project $MigrationsProj `
        --startup-project $ApiProj
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "EF update on SourceFacade lab DB failed - SPs that read Items may error at runtime; definitions still export."
    }
}

$expected = @(
    "showcase.GetShowcaseSummary",
    "showcase.usp_Showcase_Sales_Summary",
    "showcase.usp_Showcase_Sales_Detail",
    "showcase.usp_Showcase_Inventory_Summary",
    "showcase.usp_Showcase_Inventory_Detail"
)

if (-not $SkipExport) {
    Write-Step "Export stored procedures"
    if ([string]::IsNullOrWhiteSpace($ExportDirectory)) {
        $ExportDirectory = Join-Path $env:TEMP "showcase-sp-export"
    }
    if (-not (Test-Path -LiteralPath $ExportDirectory)) {
        New-Item -ItemType Directory -Path $ExportDirectory -Force | Out-Null
    }

    $exportScript = Join-Path $DbIntelScripts "Export-DatabaseStoredProcedures.ps1"
    if (-not (Test-Path -LiteralPath $exportScript)) {
        throw "Export script not found: $exportScript"
    }

    $ownedSql = Join-Path $ExportDirectory "ShowcaseOwned-procedures.sql"
    $ownedList = Join-Path $ExportDirectory "ShowcaseOwned-sp-list.txt"
    $sourceList = Join-Path $ExportDirectory "ShowcaseSource-sp-list.txt"

    & $exportScript -OutputFile $ownedSql -UseShowcaseLocalDefaults -Endpoint Owned -Schema showcase
    & $exportScript -OutputFile $ownedList -UseShowcaseLocalDefaults -Endpoint Owned -Schema showcase -ListOnly
    & $exportScript -OutputFile $sourceList -UseShowcaseLocalDefaults -Endpoint SourceFacade -Schema showcase -ListOnly

    $list = Get-Content -LiteralPath $ownedList | Where-Object { $_ -and -not $_.StartsWith("#") }
    Write-Host "Owned SP list ($($list.Count)):" -ForegroundColor Green
    $list | ForEach-Object { Write-Host "  $_" }

    $missing = @()
    foreach ($name in $expected) {
        if (-not ($list | Where-Object { $_.Equals($name, [StringComparison]::OrdinalIgnoreCase) })) {
            $missing += $name
        }
    }
    if ($missing.Count -gt 0) {
        throw "Export missing expected procedures: $($missing -join ', ')"
    }

    Write-Host "Export artifacts:" -ForegroundColor Green
    Write-Host "  $ownedSql"
    Write-Host "  $ownedList"
    Write-Host "  $sourceList"
}

Write-Step "Done"
Write-Host ("LocalDB lab ready: {0} + {1} on {2}" -f $OwnedDb, $SourceDb, $Server) -ForegroundColor Green
Write-Host 'Next: ..\..\DbIntelligence\scripts\Invoke-DbIntelligenceExtractSps.ps1 -UseShowcaseLocalDefaults -SkipCodeTools'
exit 0
