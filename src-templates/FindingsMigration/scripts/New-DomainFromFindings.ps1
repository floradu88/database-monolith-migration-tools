#Requires -Version 5.1
<#
.SYNOPSIS
  Scaffold a DataService project from the ShowcaseDataService golden template using a findings package.

.PARAMETER DomainName
  Domain name used for replacements (e.g. Insight → InsightDataService).

.PARAMETER PackageDirectory
  Output of Invoke-FindingsMigration.ps1 (contains domain-package.json / SCAFFOLD.md).

.PARAMETER ServiceName
  Override service folder/name (default: {Domain}DataService).

.PARAMETER StoredProcedureMap
  Optional path to stored-procedure-map.json — generates SQL stubs + C# Sp_* wrappers.

.PARAMETER CopyManifestsToKit
  Copy generated manifests into kit manifests/domains|migration-waves|objects.

.EXAMPLE
  .\New-DomainFromFindings.ps1 -DomainName Insight -PackageDirectory .\out\Insight -CopyManifestsToKit
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$DomainName,
    [Parameter(Mandatory = $true)]
    [string]$PackageDirectory,
    [string]$ServiceName = "",
    [string]$StoredProcedureMap = "",
    [switch]$ParallelDboCore,
    [string]$SourceSchema = "dbo",
    [string]$OwnedSchema = "core",
    [switch]$CopyManifestsToKit,
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$FindingsRoot = Split-Path -Parent $PSScriptRoot
$SrcTemplates = Split-Path -Parent $FindingsRoot
$KitRoot = Split-Path -Parent $SrcTemplates
$Template = Join-Path $SrcTemplates "DataServices\ShowcaseDataService"

if (-not (Test-Path $Template)) { throw "Golden template missing: $Template" }
if (-not (Test-Path $PackageDirectory)) { throw "Package directory missing: $PackageDirectory" }

if (-not $ServiceName) { $ServiceName = "${DomainName}DataService" }
$dest = Join-Path $SrcTemplates "DataServices\$ServiceName"

if ((Test-Path $dest) -and -not $Force) {
    throw "Destination exists: $dest (pass -Force to replace)"
}
if (Test-Path $dest) {
    Remove-Item $dest -Recurse -Force
}

Write-Host "Copying ShowcaseDataService → $dest" -ForegroundColor Cyan
Copy-Item $Template $dest -Recurse

# Rename folders/files containing ShowcaseDataService
Get-ChildItem $dest -Recurse | Sort-Object FullName -Descending | ForEach-Object {
    if ($_.Name -like "*ShowcaseDataService*") {
        $newName = $_.Name -replace "ShowcaseDataService", $ServiceName
        Rename-Item -LiteralPath $_.FullName -NewName $newName
    }
}

$schemaLower = $DomainName.ToLowerInvariant()

# Replace text inside common text files
$patterns = @("*.cs", "*.csproj", "*.sqlproj", "*.md", "*.json", "*.yml", "*.yaml", "*.sql", "*.html")
foreach ($pat in $patterns) {
    Get-ChildItem $dest -Recurse -Filter $pat -File -ErrorAction SilentlyContinue | ForEach-Object {
        $text = Get-Content -LiteralPath $_.FullName -Raw -ErrorAction SilentlyContinue
        if ($null -eq $text) { return }
        $updated = $text `
            -replace "ShowcaseDataService", $ServiceName `
            -replace "ShowcaseDb", "${DomainName}Db" `
            -replace "showcase\.", ($schemaLower + ".") `
            -replace "\bShowcase\b", $DomainName `
            -replace "\bshowcase\b", $schemaLower
        if ($updated -ne $text) {
            Set-Content -LiteralPath $_.FullName -Value $updated -Encoding UTF8 -NoNewline
        }
    }
}

$readme = @"
# $ServiceName

Scaffolded from ``ShowcaseDataService`` (golden DB-as-a-Service template) via FindingsMigration.

- Domain: $DomainName
- Findings package: $PackageDirectory
- Review ``FINDINGS-REVIEW.md`` in the package before claiming ownership.
- Keep SQL database project ownership and EF migrations ownership non-overlapping.
- FacadeThenMove: SourceFacade (Blue) → Shadow → ParallelWrite (dbo+core) → Owned (Green). See SHOWCASE-CUTOVER.md patterns.

Generated: $(Get-Date -Format o)
"@
Set-Content -Path (Join-Path $dest "README.md") -Value $readme -Encoding UTF8

# Optional SP wrapper generation
$spMapPath = $StoredProcedureMap
if (-not $spMapPath) {
    $candidate = Join-Path $PackageDirectory "stored-procedure-map.json"
    if (Test-Path $candidate) { $spMapPath = $candidate }
}

if ($spMapPath -and (Test-Path $spMapPath)) {
    Write-Host "Generating SP stubs/wrappers from $spMapPath" -ForegroundColor Cyan
    $genScript = Join-Path $PSScriptRoot "New-SpWrappersFromMap.ps1"
    if (-not (Test-Path $genScript)) { throw "Missing $genScript" }
    $parallelSplat = @{}
    if ($ParallelDboCore) {
        $parallelSplat["ParallelDboCore"] = $true
        $parallelSplat["SourceSchema"] = $SourceSchema
        $parallelSplat["OwnedSchema"] = $OwnedSchema
    }
    & $genScript -StoredProcedureMap $spMapPath -ServiceRoot $dest -DomainName $DomainName -ServiceName $ServiceName -TargetSchema $schemaLower @parallelSplat
}

if ($CopyManifestsToKit) {
    $kitManifests = Join-Path $KitRoot "manifests"
    foreach ($rel in @("domains", "migration-waves", "objects")) {
        $src = Join-Path $PackageDirectory "manifests\$rel"
        $dst = Join-Path $kitManifests $rel
        if (-not (Test-Path $src)) { continue }
        New-Item -ItemType Directory -Path $dst -Force | Out-Null
        Copy-Item (Join-Path $src "*") $dst -Force
        Write-Host "Copied manifests/$rel → $dst"
    }
}

Write-Host "Scaffold complete: $dest" -ForegroundColor Green
Write-Host "Open FINDINGS-REVIEW.md in the package and obtain ownership approval before cutover."
Write-Host "CustomerDataService remains a thin example — ShowcaseDataService is the golden template."
exit 0
