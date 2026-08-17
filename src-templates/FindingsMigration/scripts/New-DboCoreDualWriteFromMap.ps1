#Requires -Version 5.1
<#
.SYNOPSIS
  Generate dbo → core parallel-write artifacts from stored-procedure-map.json.

.DESCRIPTION
  Wrapper around New-SpWrappersFromMap.ps1 -ParallelDboCore.
  Emits core table clones, core SP stubs, ParallelWrite C# helper, and cutover register scripts.
  Does not run SQL. DBA review required. Delta-only (no historical backfill).
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
    [string]$SourceSchema = "dbo",
    [string]$OwnedSchema = "core"
)

$ErrorActionPreference = "Stop"
$inner = Join-Path $PSScriptRoot "New-SpWrappersFromMap.ps1"
if (-not (Test-Path $inner)) { throw "Missing $inner" }

& $inner `
    -StoredProcedureMap $StoredProcedureMap `
    -ServiceRoot $ServiceRoot `
    -DomainName $DomainName `
    -ServiceName $ServiceName `
    -TargetSchema $OwnedSchema `
    -ParallelDboCore `
    -SourceSchema $SourceSchema `
    -OwnedSchema $OwnedSchema

Write-Host "Next: review DBO-CORE-PARALLEL-WRITE.md, deploy sql/common/40-45 (DBA), set business keys, enable X-Data-Access-Route: ParallelWrite." -ForegroundColor Green
