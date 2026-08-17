#Requires -Version 5.1
<#
.SYNOPSIS
  Generate SQL SP stubs and C# Dapper wrappers into a DataService from stored-procedure-map.json.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$StoredProcedureMap,
    [Parameter(Mandatory = $true)]
    [string]$ServiceRoot,
    [Parameter(Mandatory = $true)]
    [string]$DomainName,
    [Parameter(Mandatory = $true)]
    [string]$ServiceName,
    [string]$TargetSchema = "",
    [switch]$ParallelDboCore,
    [string]$SourceSchema = "dbo",
    [string]$OwnedSchema = "core"
)

$ErrorActionPreference = "Stop"
$FindingsRoot = Split-Path -Parent $PSScriptRoot
$cli = Join-Path $FindingsRoot "FindingsMigration.Cli\FindingsMigration.Cli.csproj"
if (-not (Test-Path $cli)) { throw "CLI project missing: $cli" }
if (-not (Test-Path $StoredProcedureMap)) { throw "SP map missing: $StoredProcedureMap" }
if (-not (Test-Path $ServiceRoot)) { throw "Service root missing: $ServiceRoot" }

$schemaArg = if ($TargetSchema) { @("--schema", $TargetSchema) } else { @() }
$parallelArg = if ($ParallelDboCore) { @("--parallel-dbo-core", "--source-schema", $SourceSchema, "--owned-schema", $OwnedSchema) } else { @() }

& dotnet run --project $cli -c Release -- `
    generate-sp `
    --sp-map $StoredProcedureMap `
    --service-root $ServiceRoot `
    --domain $DomainName `
    --service $ServiceName `
    @schemaArg `
    @parallelArg

if ($LASTEXITCODE -ne 0) { throw "generate-sp failed with exit $LASTEXITCODE" }
Write-Host "SP wrappers generated under $ServiceRoot" -ForegroundColor Green
