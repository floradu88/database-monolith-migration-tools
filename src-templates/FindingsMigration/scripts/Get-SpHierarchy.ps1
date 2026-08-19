#Requires -Version 5.1
<#
.SYNOPSIS
  Produce a stored-procedure dependency hierarchy (tables + used/unused columns, types, and other SPs).

.DESCRIPTION
  - Always calls FindingsMigration.Cli `sp-hierarchy`.
  - Optionally generates an inventory JSON snapshot by executing:
      sql/common/50-sp-dependency-hierarchy.sql

.PARAMETER StoredProcedureMap
  Path to FindingsMigration stored-procedure-map.json.

.PARAMETER SpName
  Root stored procedure FQN (e.g. dbo.usp_GetCustomerSummary).

.PARAMETER Inventory
  Optional path to an inventory JSON snapshot produced by sql/common/50-sp-dependency-hierarchy.sql.

.PARAMETER SqlConnectionString
  When provided AND -Inventory is missing, the script will execute sql/common/50-sp-dependency-hierarchy.sql
  to generate the inventory JSON and pass it to the CLI.

.PARAMETER SqlScriptPath
  Optional override for the SQL script path. Default: repository sql/common/50-sp-dependency-hierarchy.sql

.PARAMETER Format
  tree | json (default json).

.PARAMETER OutFile
  Optional output file path for CLI output.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$StoredProcedureMap,

    [Parameter(Mandatory = $true)]
    [string]$SpName,

    [string]$Inventory = "",

    [string]$SqlConnectionString = "",

    [string]$SqlScriptPath = "",

    [ValidateSet("tree", "json")]
    [string]$Format = "json",

    [string]$OutFile = ""
)

$ErrorActionPreference = "Stop"

$FindingsRoot = Split-Path -Parent $PSScriptRoot
$Cli = Join-Path $FindingsRoot "FindingsMigration.Cli\FindingsMigration.Cli.csproj"

if (-not (Test-Path $StoredProcedureMap)) { throw "SP map missing: $StoredProcedureMap" }
if (-not (Test-Path $Cli)) { throw "CLI csproj missing: $Cli" }

$repoRoot = Split-Path -Parent (Split-Path -Parent $FindingsRoot)
if ([string]::IsNullOrWhiteSpace($SqlScriptPath)) {
    $SqlScriptPath = Join-Path $repoRoot "sql/common/50-sp-dependency-hierarchy.sql"
}
if (-not (Test-Path $SqlScriptPath)) { throw "SQL script missing: $SqlScriptPath" }

$inventoryWasGenerated = $false

if ([string]::IsNullOrWhiteSpace($Inventory)) {
    if (-not [string]::IsNullOrWhiteSpace($SqlConnectionString)) {
        $inventoryWasGenerated = $true
        $tempDir = Join-Path $env:TEMP ("sp-hierarchy-" + [guid]::NewGuid().ToString("N"))
        New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
        $Inventory = Join-Path $tempDir "inventory.json"
    }
}

if (-not [string]::IsNullOrWhiteSpace($SqlConnectionString) -and -not (Test-Path $Inventory)) {
    Write-Host "Generating inventory via 50-sp-dependency-hierarchy.sql ..." -ForegroundColor Cyan

    # Read the whole SQL script as the command batch.
    $sqlText = Get-Content -LiteralPath $SqlScriptPath -Raw

    # Prefer Microsoft.Data.SqlClient if available; else fall back.
    $connection = $null
    try {
        $connection = New-Object Microsoft.Data.SqlClient.SqlConnection $SqlConnectionString
    }
    catch {
        $connection = New-Object System.Data.SqlClient.SqlConnection $SqlConnectionString
    }

    $connection.Open()
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = $sqlText
    $cmd.CommandTimeout = 300

    $param = $cmd.Parameters.Add("@SpName", [System.Data.SqlDbType]::NVarChar, 256)
    $param.Value = $SpName

    $reader = $cmd.ExecuteReader()

    function Read-ResultSet($r) {
        $rows = @()
        while ($r.Read()) {
            $obj = [ordered]@{}
            for ($i = 0; $i -lt $r.FieldCount; $i++) {
                $col = $r.GetName($i)
                $obj[$col] = $r.GetValue($i)
            }
            $rows += [pscustomobject]$obj
        }
        return ,$rows
    }

    $resultSets = @()
    do {
        $resultSets += ,(Read-ResultSet $reader)
    } while ($reader.NextResult())

    $reader.Close()
    $connection.Close()

    # SQL script returns result sets in fixed order:
    # 1) ProcedureEdges
    # 2) TableColumnUsage
    # 3) TypeDependencies
    # 4) ViewDependencies
    $procedureEdges = if ($resultSets.Count -ge 1) { $resultSets[0] } else { @() }
    $tableColumnUsage = if ($resultSets.Count -ge 2) { $resultSets[1] } else { @() }
    $typeDependencies = if ($resultSets.Count -ge 3) { $resultSets[2] } else { @() }
    $viewDependencies = if ($resultSets.Count -ge 4) { $resultSets[3] } else { @() }

    $invObj = [pscustomobject]@{
        generatedAt = (Get-Date).ToString("o")
        rootProcedure = $SpName
        procedureEdges = $procedureEdges
        tableColumnUsage = $tableColumnUsage
        typeDependencies = $typeDependencies
        viewDependencies = $viewDependencies
    }

    $invJson = $invObj | ConvertTo-Json -Depth 12
    Set-Content -LiteralPath $Inventory -Value $invJson -Encoding UTF8

    Write-Host "Inventory written to: $Inventory" -ForegroundColor Green
}

$argList = @(
    "run", "--project", $Cli, "-c", "Release", "--",
    "sp-hierarchy",
    "--sp-map", (Resolve-Path $StoredProcedureMap).Path,
    "--sp-name", $SpName,
    "--format", $Format
)

if (-not [string]::IsNullOrWhiteSpace($Inventory) -and (Test-Path $Inventory)) {
    $argList += @("--inventory", (Resolve-Path $Inventory).Path)
}

if (-not [string]::IsNullOrWhiteSpace($OutFile)) {
    $argList += @("--out", $OutFile)
}

& dotnet @argList
$LASTEXITCODE = $LASTEXITCODE
exit $LASTEXITCODE

