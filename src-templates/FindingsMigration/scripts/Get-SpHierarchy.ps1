#Requires -Version 5.1
<#
.SYNOPSIS
  Produce a stored-procedure dependency hierarchy (tables + used/unused columns, types, and other SPs).

.DESCRIPTION
  Calls FindingsMigration.Cli `sp-hierarchy` to analyze an SP's dependency tree.

  When -SqlConnectionString is provided and -Inventory is not, the script first
  runs Export-SpDependencyInventory.ps1 to extract the inventory from the live
  database, then passes the resulting JSON to the CLI.

.PARAMETER StoredProcedureMap
  Path to FindingsMigration stored-procedure-map.json.

.PARAMETER SpName
  Root stored procedure FQN (e.g. dbo.usp_GetCustomerSummary).

.PARAMETER Inventory
  Optional path to an inventory JSON snapshot produced by Export-SpDependencyInventory.ps1
  (or sql/common/50-sp-dependency-hierarchy.sql).

.PARAMETER SqlConnectionString
  When provided AND -Inventory is not set, the script calls Export-SpDependencyInventory.ps1
  to generate the inventory JSON from the live database.

.PARAMETER UseShowcaseLocalDefaults
  Infer LocalDB connection from Showcase appsettings.json (passed to Export-SpDependencyInventory.ps1).

.PARAMETER Format
  tree | json (default json).

.PARAMETER OutFile
  Optional output file path for CLI output.

.EXAMPLE
  .\Get-SpHierarchy.ps1 `
    -StoredProcedureMap "D:\project\.db-index\stored-procedure-map.json" `
    -SpName "dbo.usp_GetCustomerSummary" `
    -SqlConnectionString "Server=.;Database=Monolith;Trusted_Connection=True;TrustServerCertificate=True" `
    -Format tree

.EXAMPLE
  .\Get-SpHierarchy.ps1 `
    -StoredProcedureMap "D:\project\.db-index\stored-procedure-map.json" `
    -SpName "dbo.usp_GetCustomerSummary" `
    -Inventory "D:\exports\customer-summary-inventory.json" `
    -Format json
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$StoredProcedureMap,

    [Parameter(Mandatory = $true)]
    [string]$SpName,

    [string]$Inventory = "",

    [string]$SqlConnectionString = "",

    [switch]$UseShowcaseLocalDefaults,

    [ValidateSet("tree", "json")]
    [string]$Format = "json",

    [string]$OutFile = ""
)

$ErrorActionPreference = "Stop"

$FindingsRoot = Split-Path -Parent $PSScriptRoot
$Cli = Join-Path $FindingsRoot "FindingsMigration.Cli\FindingsMigration.Cli.csproj"

if (-not (Test-Path $StoredProcedureMap)) { throw "SP map missing: $StoredProcedureMap" }
if (-not (Test-Path $Cli)) { throw "CLI csproj missing: $Cli" }

# ── Auto-extract inventory from DB when no inventory file is provided ──
if ([string]::IsNullOrWhiteSpace($Inventory)) {
    $needsExtract = (-not [string]::IsNullOrWhiteSpace($SqlConnectionString)) -or $UseShowcaseLocalDefaults
    if ($needsExtract) {
        $tempDir = Join-Path $env:TEMP ("sp-hierarchy-" + [guid]::NewGuid().ToString("N"))
        New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
        $Inventory = Join-Path $tempDir "inventory.json"

        $exportScript = Join-Path $PSScriptRoot "Export-SpDependencyInventory.ps1"
        if (-not (Test-Path -LiteralPath $exportScript)) {
            throw "Export script missing: $exportScript"
        }

        $exportArgs = @{
            SpName     = $SpName
            OutputFile = $Inventory
        }
        if (-not [string]::IsNullOrWhiteSpace($SqlConnectionString)) {
            $exportArgs["SqlConnectionString"] = $SqlConnectionString
        }
        if ($UseShowcaseLocalDefaults) {
            $exportArgs["UseShowcaseLocalDefaults"] = $true
        }

        & $exportScript @exportArgs
        if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {
            throw "Export-SpDependencyInventory.ps1 failed with exit $LASTEXITCODE"
        }
    }
}

# ── Call FindingsMigration.Cli sp-hierarchy ─────────────────────────
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
exit $LASTEXITCODE
