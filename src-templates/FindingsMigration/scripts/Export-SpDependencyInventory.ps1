#Requires -Version 5.1
<#
.SYNOPSIS
  Extract a stored-procedure dependency inventory from a live SQL Server database.

.DESCRIPTION
  Runs sql/common/50-sp-dependency-hierarchy.sql against the target database for a
  given stored procedure and writes a JSON inventory file containing:
    - procedureEdges   (recursive sub-SP call graph)
    - tableColumnUsage (per-procedure, per-table, per-column with IsUsed flag)
    - typeDependencies (user-defined types referenced by SPs in the closure)
    - viewDependencies (views referenced by SPs in the closure)

  The output JSON is the format consumed by:
    findings-migrate sp-hierarchy --inventory <file>

  Read-only. Never runs destructive SQL. Does not invent credentials.

  Connection resolution (first wins):
    1. -SqlConnectionString
    2. $env:DbIntelligence__SqlConnectionString
    3. -UseShowcaseLocalDefaults → Showcase LocalDB from kit appsettings.json

.PARAMETER SpName
  Root stored procedure fully-qualified name (e.g. dbo.usp_GetCustomerSummary).

.PARAMETER OutputFile
  Path to write the inventory JSON (required). Parent directory is created if missing.

.PARAMETER SqlConnectionString
  SQL Server connection string. Database must already be set in the string.

.PARAMETER UseShowcaseLocalDefaults
  Infer LocalDB Owned connection from Showcase appsettings.json.

.PARAMETER Endpoint
  Owned (default) or SourceFacade when using Showcase defaults.

.PARAMETER SqlScriptPath
  Override for the SQL script path. Default: repository sql/common/50-sp-dependency-hierarchy.sql.

.EXAMPLE
  .\Export-SpDependencyInventory.ps1 `
    -SpName "dbo.usp_GetCustomerSummary" `
    -OutputFile "D:\exports\customer-summary-inventory.json" `
    -SqlConnectionString "Server=.;Database=Monolith;Trusted_Connection=True;TrustServerCertificate=True"

.EXAMPLE
  .\Export-SpDependencyInventory.ps1 `
    -SpName "showcase.usp_Showcase_Sales_Summary" `
    -OutputFile ".\inventory.json" `
    -UseShowcaseLocalDefaults
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SpName,

    [Parameter(Mandatory = $true)]
    [Alias("Path", "FilePath")]
    [string]$OutputFile,

    [string]$SqlConnectionString = "",

    [switch]$UseShowcaseLocalDefaults,

    [ValidateSet("Owned", "SourceFacade")]
    [string]$Endpoint = "Owned",

    [string]$SqlScriptPath = ""
)

$ErrorActionPreference = "Stop"

# ── Resolve connection string ───────────────────────────────────────
$DbIntelligenceScripts = Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) `
    "DbIntelligence\scripts"
$resolverPath = Join-Path $DbIntelligenceScripts "Resolve-DbIntelligenceSqlConnection.ps1"
if (Test-Path -LiteralPath $resolverPath) {
    . $resolverPath
    $cs = Resolve-DbIntelligenceSqlConnection `
        -SqlConnectionString $SqlConnectionString `
        -UseShowcaseLocalDefaults:$UseShowcaseLocalDefaults `
        -Endpoint $Endpoint
} elseif (-not [string]::IsNullOrWhiteSpace($SqlConnectionString)) {
    $cs = $SqlConnectionString.Trim()
} elseif (-not [string]::IsNullOrWhiteSpace($env:DbIntelligence__SqlConnectionString)) {
    $cs = $env:DbIntelligence__SqlConnectionString.Trim()
} else {
    $cs = ""
}

if ([string]::IsNullOrWhiteSpace($cs)) {
    throw @"
No SqlConnectionString resolved.
Provide -SqlConnectionString, set `$env:DbIntelligence__SqlConnectionString, or pass -UseShowcaseLocalDefaults.
"@
}

# ── Resolve SQL script path ─────────────────────────────────────────
$repoRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
if ([string]::IsNullOrWhiteSpace($SqlScriptPath)) {
    $SqlScriptPath = Join-Path $repoRoot "sql\common\50-sp-dependency-hierarchy.sql"
}
if (-not (Test-Path -LiteralPath $SqlScriptPath)) {
    throw "SQL script not found: $SqlScriptPath"
}

# ── Resolve output path ─────────────────────────────────────────────
if (-not [System.IO.Path]::IsPathRooted($OutputFile)) {
    $OutputFile = Join-Path (Get-Location).Path $OutputFile
}
$OutputFile = [System.IO.Path]::GetFullPath($OutputFile)
$parentDir = Split-Path -Parent $OutputFile
if (-not [string]::IsNullOrWhiteSpace($parentDir) -and -not (Test-Path -LiteralPath $parentDir)) {
    New-Item -ItemType Directory -Path $parentDir -Force | Out-Null
}

# ── Connect and execute ─────────────────────────────────────────────
$safeCs = [regex]::Replace($cs, '(?i)(Password|Pwd)=[^;]*', '$1=***')
Write-Host "Connecting (read-only inventory) ..." -ForegroundColor Cyan
Write-Host "Connection (redacted): $safeCs"
Write-Host "Stored procedure:      $SpName"

$sqlText = Get-Content -LiteralPath $SqlScriptPath -Raw

$connection = $null
try {
    $connection = New-Object Microsoft.Data.SqlClient.SqlConnection $cs
}
catch {
    $connection = New-Object System.Data.SqlClient.SqlConnection $cs
}

$connection.Open()
$cmd = $connection.CreateCommand()
$cmd.CommandText = $sqlText
$cmd.CommandTimeout = 300

$param = $cmd.Parameters.Add("@SpName", [System.Data.SqlDbType]::NVarChar, 256)
$param.Value = $SpName

$reader = $null
try {
    $reader = $cmd.ExecuteReader()
}
catch {
    $connection.Close()
    $connection.Dispose()
    throw "SQL execution failed: $_"
}

function Read-ResultSet($r) {
    $rows = [System.Collections.Generic.List[pscustomobject]]::new()
    while ($r.Read()) {
        $obj = [ordered]@{}
        for ($i = 0; $i -lt $r.FieldCount; $i++) {
            $col = $r.GetName($i)
            $val = $r.GetValue($i)
            if ($val -is [System.DBNull]) { $val = $null }
            $obj[$col] = $val
        }
        $rows.Add([pscustomobject]$obj)
    }
    return ,$rows
}

$resultSets = [System.Collections.Generic.List[object]]::new()
do {
    $resultSets.Add((Read-ResultSet $reader))
} while ($reader.NextResult())

$reader.Close()
$connection.Close()
$connection.Dispose()

$procedureEdges   = if ($resultSets.Count -ge 1) { $resultSets[0] } else { @() }
$tableColumnUsage = if ($resultSets.Count -ge 2) { $resultSets[1] } else { @() }
$typeDependencies = if ($resultSets.Count -ge 3) { $resultSets[2] } else { @() }
$viewDependencies     = if ($resultSets.Count -ge 4) { $resultSets[3] } else { @() }
$functionDependencies = if ($resultSets.Count -ge 5) { $resultSets[4] } else { @() }

Write-Host ""
Write-Host "Results:" -ForegroundColor Green
Write-Host "  Procedure edges:       $($procedureEdges.Count)"
Write-Host "  Table column rows:     $($tableColumnUsage.Count)"
Write-Host "  Type dependencies:     $($typeDependencies.Count)"
Write-Host "  View dependencies:     $($viewDependencies.Count)"
Write-Host "  Function dependencies: $($functionDependencies.Count)"

# ── Write inventory JSON ────────────────────────────────────────────
$inventory = [pscustomobject]@{
    generatedAt          = [DateTimeOffset]::UtcNow.ToString("o")
    rootProcedure        = $SpName
    database             = $connection.Database
    procedureEdges       = $procedureEdges
    tableColumnUsage     = $tableColumnUsage
    typeDependencies     = $typeDependencies
    viewDependencies     = $viewDependencies
    functionDependencies = $functionDependencies
}

$json = $inventory | ConvertTo-Json -Depth 12
[System.IO.File]::WriteAllText($OutputFile, $json, [System.Text.UTF8Encoding]::new($false))

Write-Host ""
Write-Host "Inventory written to: $OutputFile" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  # View hierarchy (requires stored-procedure-map.json):"
Write-Host "  .\Get-SpHierarchy.ps1 -StoredProcedureMap <sp-map.json> -SpName `"$SpName`" -Inventory `"$OutputFile`" -Format tree"
Write-Host ""
Write-Host "  # Or use the CLI directly:"
Write-Host "  dotnet run --project FindingsMigration.Cli -c Release -- sp-hierarchy --sp-map <sp-map.json> --sp-name `"$SpName`" --inventory `"$OutputFile`" --format tree"

exit 0
