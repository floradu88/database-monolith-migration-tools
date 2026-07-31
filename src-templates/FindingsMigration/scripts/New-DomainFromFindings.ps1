#Requires -Version 5.1
<#
.SYNOPSIS
  Scaffold a DataService project from the CustomerDataService template using a findings package.

.PARAMETER DomainName
  Domain name used for replacements (e.g. Insight → InsightDataService).

.PARAMETER PackageDirectory
  Output of Invoke-FindingsMigration.ps1 (contains domain-package.json / SCAFFOLD.md).

.PARAMETER ServiceName
  Override service folder/name (default: {Domain}DataService).

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
    [switch]$CopyManifestsToKit,
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$FindingsRoot = Split-Path -Parent $PSScriptRoot
$SrcTemplates = Split-Path -Parent $FindingsRoot
$KitRoot = Split-Path -Parent $SrcTemplates
$Template = Join-Path $SrcTemplates "DataServices\CustomerDataService"

if (-not (Test-Path $Template)) { throw "Template missing: $Template" }
if (-not (Test-Path $PackageDirectory)) { throw "Package directory missing: $PackageDirectory" }

if (-not $ServiceName) { $ServiceName = "${DomainName}DataService" }
$dest = Join-Path $SrcTemplates "DataServices\$ServiceName"

if ((Test-Path $dest) -and -not $Force) {
    throw "Destination exists: $dest (pass -Force to replace)"
}
if (Test-Path $dest) {
    Remove-Item $dest -Recurse -Force
}

Write-Host "Copying CustomerDataService → $dest" -ForegroundColor Cyan
Copy-Item $Template $dest -Recurse

# Rename folders/files containing CustomerDataService
Get-ChildItem $dest -Recurse | Sort-Object FullName -Descending | ForEach-Object {
    if ($_.Name -like "*CustomerDataService*") {
        $newName = $_.Name -replace "CustomerDataService", $ServiceName
        Rename-Item -LiteralPath $_.FullName -NewName $newName
    }
}

# Replace text inside common text files
$patterns = @("*.cs", "*.csproj", "*.md", "*.json", "*.yml", "*.yaml")
foreach ($pat in $patterns) {
    Get-ChildItem $dest -Recurse -Filter $pat -File -ErrorAction SilentlyContinue | ForEach-Object {
        $text = Get-Content -LiteralPath $_.FullName -Raw -ErrorAction SilentlyContinue
        if ($null -eq $text) { return }
        $updated = $text `
            -replace "CustomerDataService", $ServiceName `
            -replace "CustomerDb", "${DomainName}Db" `
            -replace "customer\.", ($DomainName.ToLowerInvariant() + ".") `
            -replace "\bcustomer\b", $DomainName.ToLowerInvariant()
        if ($updated -ne $text) {
            Set-Content -LiteralPath $_.FullName -Value $updated -Encoding UTF8 -NoNewline
        }
    }
}

$readme = @"
# $ServiceName

Scaffolded from ``CustomerDataService`` via FindingsMigration.

- Domain: $DomainName
- Findings package: $PackageDirectory
- Review ``FINDINGS-REVIEW.md`` in the package before claiming ownership.
- Keep SQL database project ownership and EF migrations ownership non-overlapping.

Generated: $(Get-Date -Format o)
"@
Set-Content -Path (Join-Path $dest "README.md") -Value $readme -Encoding UTF8

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
exit 0
