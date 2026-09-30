#Requires -Version 5.1
<#
.SYNOPSIS
  Build a .dacpac from a .sqlproj (SDK-style Microsoft.Build.Sql or classic SSDT), or extract one with SqlPackage.

.DESCRIPTION
  Path-only Ready entrypoint. Uses the same VS / .NET tools Visual Studio uses:

  - SDK-style (`Sdk="Microsoft.Build.Sql"`): `dotnet build`
  - Classic SSDT (SqlTasks.targets): VS `MSBuild.exe` (via vswhere)
  - Extract from a live SQL Server database: VS / PATH `SqlPackage.exe` `/Action:Extract`

  Does not publish to a database. Never pass production connection strings without DBA review.

.PARAMETER ProjectPath
  Path to a `.sqlproj` file, or a folder containing exactly one `.sqlproj`. Aliases: Path, SqlProject.

.PARAMETER Configuration
  Build configuration. Default Debug.

.PARAMETER Output
  Output directory for build, or full `.dacpac` path for -Extract.

.PARAMETER Intermediate
  Optional intermediate/obj directory (useful when C: is low on space).

.PARAMETER Extract
  Extract a dacpac from a database instead of building a .sqlproj.
  Requires -ConnectionString (or MIGRATION_CONNECTION_STRING) and -Output ending in .dacpac.

.PARAMETER ConnectionString
  Source connection string for -Extract. Prefer env MIGRATION_CONNECTION_STRING.

.PARAMETER Open
  Open the output folder after success.

.PARAMETER CheckPrereqs
  Print DACPAC tool prerequisites (dotnet / MSBuild / SSDT / SqlPackage) and exit.

.EXAMPLE
  .\Invoke-DacpacReady.ps1 -CheckPrereqs

.EXAMPLE
  .\Invoke-DacpacReady.ps1 "C:\code\projects\...\ShowcaseDataService.Database.sqlproj"

.EXAMPLE
  .\Invoke-DacpacReady.ps1 ".\fixtures\DacpacFixture" -Output "D:\dacpac-out"

.EXAMPLE
  .\Invoke-DacpacReady.ps1 -Extract -Output "D:\out\live.dacpac"
#>
[CmdletBinding(DefaultParameterSetName = "Build")]
param(
    [Parameter(Mandatory = $true, Position = 0, ParameterSetName = "Build")]
    [Alias("Path", "SqlProject")]
    [string]$ProjectPath,

    [Parameter(ParameterSetName = "Build")]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [Parameter(ParameterSetName = "Build")]
    [Parameter(Mandatory = $true, ParameterSetName = "Extract")]
    [string]$Output = "",

    [Parameter(ParameterSetName = "Build")]
    [string]$Intermediate = "",

    [Parameter(Mandatory = $true, ParameterSetName = "Extract")]
    [switch]$Extract,

    [Parameter(ParameterSetName = "Extract")]
    [string]$ConnectionString = "",

    [Parameter(ParameterSetName = "Prereqs")]
    [switch]$CheckPrereqs,

    [switch]$Open
)

$ErrorActionPreference = "Stop"
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$HostProject = Join-Path $RepoRoot "src\MigrationTool.Host\MigrationTool.Host.csproj"

if (-not (Test-Path -LiteralPath $HostProject)) {
    throw "MigrationTool.Host not found at $HostProject"
}

function Resolve-SqlProject([string]$PathOrFolder) {
    if (-not (Test-Path -LiteralPath $PathOrFolder)) {
        throw "Path not found: $PathOrFolder"
    }
    $item = Get-Item -LiteralPath $PathOrFolder
    if (-not $item.PSIsContainer) {
        if ($item.Extension -ne ".sqlproj") {
            throw "Expected a .sqlproj file: $PathOrFolder"
        }
        return $item.FullName
    }
    $projects = @(Get-ChildItem -LiteralPath $item.FullName -Filter *.sqlproj -File)
    if ($projects.Count -eq 0) {
        throw "No .sqlproj under folder: $PathOrFolder"
    }
    if ($projects.Count -gt 1) {
        throw "Multiple .sqlproj files under $PathOrFolder. Pass one file path."
    }
    return $projects[0].FullName
}

& dotnet build $HostProject -c Release -v q
if ($LASTEXITCODE -ne 0) {
    throw "Failed to build MigrationTool.Host"
}

if ($CheckPrereqs) {
    & dotnet run --project $HostProject -c Release --no-build --no-launch-profile -- --dacpac-prereqs
    exit $LASTEXITCODE
}

if ($Extract) {
    if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
        $ConnectionString = $env:MIGRATION_CONNECTION_STRING
    }
    if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
        throw "Set -ConnectionString or MIGRATION_CONNECTION_STRING for -Extract."
    }
    if ([string]::IsNullOrWhiteSpace($Output) -or -not $Output.EndsWith(".dacpac", [StringComparison]::OrdinalIgnoreCase)) {
        throw "-Output must be a .dacpac file path when using -Extract."
    }
    $runArgs = @("run", "--project", $HostProject, "--", "--extract-dacpac", $Output, "--connection", $ConnectionString)
    & dotnet @runArgs
    if ($LASTEXITCODE -ne 0) { throw "Extract failed." }
    if ($Open) { Invoke-Item (Split-Path -Parent (Resolve-Path $Output)) }
    Write-Host "DACPAC extracted: $Output"
    return
}

$sqlproj = Resolve-SqlProject $ProjectPath
$runArgs = @("run", "--project", $HostProject, "--", "--build-dacpac", $sqlproj, "--configuration", $Configuration)
if (-not [string]::IsNullOrWhiteSpace($Output)) {
    $runArgs += @("--output", $Output)
}
if (-not [string]::IsNullOrWhiteSpace($Intermediate)) {
    $runArgs += @("--intermediate", $Intermediate)
}

& dotnet @runArgs
if ($LASTEXITCODE -ne 0) {
    throw "DACPAC build failed."
}

if ($Open -and -not [string]::IsNullOrWhiteSpace($Output) -and (Test-Path -LiteralPath $Output)) {
    $openTarget = if ((Get-Item $Output).PSIsContainer) { $Output } else { Split-Path -Parent $Output }
    Invoke-Item $openTarget
}

Write-Host "DACPAC build finished for $sqlproj"
