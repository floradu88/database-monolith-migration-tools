#Requires -Version 5.1
<#
.SYNOPSIS
  Package DbIntelligence JSON maps into domain manifests (FindingsMigration.Cli).

.PARAMETER CodeToDbMap
  Path to code-to-db-map.json

.PARAMETER StoredProcedureMap
  Optional path to stored-procedure-map.json

.PARAMETER DomainName
  Domain name (e.g. Billing)

.PARAMETER OutputDirectory
  Where to write the package (default: .\out\<Domain>)

.EXAMPLE
  .\Invoke-FindingsMigration.ps1 `
    -CodeToDbMap "D:\code\projects\personalinsightanalysis\.db-index\code-to-db-map.json" `
    -DomainName "Insight" `
    -OwnerTeam "Personal Insight"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$CodeToDbMap,
    [string]$StoredProcedureMap = "",
    [Parameter(Mandatory = $true)]
    [string]$DomainName,
    [string]$ServiceName = "",
    [string]$Schema = "",
    [string]$SourceDatabase = "MonolithDb",
    [string]$TargetDatabase = "",
    [string]$OwnerTeam = "TBD",
    [string]$OutputDirectory = "",
    [switch]$IncludeAmbiguous
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Cli = Join-Path $Root "FindingsMigration.Cli\FindingsMigration.Cli.csproj"

if (-not (Test-Path $CodeToDbMap)) { throw "Map not found: $CodeToDbMap" }
$codeFull = (Resolve-Path $CodeToDbMap).Path

if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $Root "out\$DomainName"
}

$argList = @(
    "run", "--project", $Cli, "-c", "Release", "--",
    "--code-to-db", $codeFull,
    "--domain", $DomainName,
    "--out", $OutputDirectory,
    "--source-db", $SourceDatabase,
    "--owner", $OwnerTeam
)

if ($StoredProcedureMap) {
    if (-not (Test-Path $StoredProcedureMap)) { throw "SP map not found: $StoredProcedureMap" }
    $argList += @("--sp-map", (Resolve-Path $StoredProcedureMap).Path)
}
if ($ServiceName) { $argList += @("--service", $ServiceName) }
if ($Schema) { $argList += @("--schema", $Schema) }
if ($TargetDatabase) { $argList += @("--target-db", $TargetDatabase) }
if ($IncludeAmbiguous) { $argList += "--include-ambiguous" }

Write-Host "Packaging findings → $OutputDirectory" -ForegroundColor Cyan
& dotnet @argList
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Next: .\scripts\New-DomainFromFindings.ps1 -DomainName $DomainName -PackageDirectory `"$OutputDirectory`"" -ForegroundColor Green
exit 0
