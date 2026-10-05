#Requires -Version 5.1
<#
.SYNOPSIS
  Build, extract, or publish a .dacpac using VS / .NET DAC tools (no IDE required for installs).

.DESCRIPTION
  - SDK-style (`Sdk="Microsoft.Build.Sql"`): `dotnet build`
  - Classic SSDT (SqlTasks.targets): VS `MSBuild.exe` (via vswhere)
  - Extract / Publish / Script: `SqlPackage.exe`

  Publish applies schema to a target database. Prefer `--script-only` / `-ScriptOnly` first.
  Never pass production connection strings without DBA review.

.PARAMETER ProjectPath
  Path to a `.sqlproj` file, or a folder containing exactly one `.sqlproj`.

.PARAMETER Configuration
  Build configuration. Default Debug.

.PARAMETER Output
  Output directory for build, or full `.dacpac` path for -Extract / -Publish.

.PARAMETER Intermediate
  Optional intermediate/obj directory.

.PARAMETER Extract
  Extract a dacpac from a database (requires connection + -Output .dacpac).

.PARAMETER Publish
  Publish an existing .dacpac to a database (requires connection + -Output .dacpac path).

.PARAMETER ScriptOnly
  With -Publish: generate a deployment script instead of applying (SqlPackage /Action:Script).

.PARAMETER ScriptOutput
  Optional path for -ScriptOnly deploy script.

.PARAMETER AllowDataLoss
  With -Publish: set BlockOnPossibleDataLoss=False (default blocks possible data loss).

.PARAMETER ConnectionString
  Connection string for -Extract / -Publish. Prefer env MIGRATION_CONNECTION_STRING.

.PARAMETER CheckPrereqs
  Print DACPAC tool prerequisites and exit.

.PARAMETER Open
  Open the output folder after success.

.EXAMPLE
  .\Invoke-DacpacReady.ps1 -CheckPrereqs

.EXAMPLE
  .\Invoke-DacpacReady.ps1 ".\fixtures\DacpacFixture" -Output "D:\dacpac-out"

.EXAMPLE
  .\Invoke-DacpacReady.ps1 -Publish -Output "D:\dacpac-out\DacpacFixture.dacpac" -ScriptOnly
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
    [Parameter(Mandatory = $true, ParameterSetName = "Publish")]
    [string]$Output = "",

    [Parameter(ParameterSetName = "Build")]
    [string]$Intermediate = "",

    [Parameter(Mandatory = $true, ParameterSetName = "Extract")]
    [switch]$Extract,

    [Parameter(Mandatory = $true, ParameterSetName = "Publish")]
    [switch]$Publish,

    [Parameter(ParameterSetName = "Publish")]
    [switch]$ScriptOnly,

    [Parameter(ParameterSetName = "Publish")]
    [string]$ScriptOutput = "",

    [Parameter(ParameterSetName = "Publish")]
    [switch]$AllowDataLoss,

    [Parameter(ParameterSetName = "Extract")]
    [Parameter(ParameterSetName = "Publish")]
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

function Resolve-Connection([string]$Value) {
    if (-not [string]::IsNullOrWhiteSpace($Value)) { return $Value }
    $fromEnv = $env:MIGRATION_CONNECTION_STRING
    if (-not [string]::IsNullOrWhiteSpace($fromEnv)) { return $fromEnv }
    throw "Set -ConnectionString or MIGRATION_CONNECTION_STRING."
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
    $cs = Resolve-Connection $ConnectionString
    if (-not $Output.EndsWith(".dacpac", [StringComparison]::OrdinalIgnoreCase)) {
        throw "-Output must be a .dacpac file path when using -Extract."
    }
    $runArgs = @("run", "--project", $HostProject, "-c", "Release", "--no-build", "--no-launch-profile", "--", "--extract-dacpac", $Output, "--connection", $cs)
    & dotnet @runArgs
    if ($LASTEXITCODE -ne 0) { throw "Extract failed." }
    if ($Open) { Invoke-Item (Split-Path -Parent (Resolve-Path $Output)) }
    Write-Host "DACPAC extracted: $Output"
    return
}

if ($Publish) {
    $cs = Resolve-Connection $ConnectionString
    if (-not (Test-Path -LiteralPath $Output) -or -not $Output.EndsWith(".dacpac", [StringComparison]::OrdinalIgnoreCase)) {
        throw "-Output must be an existing .dacpac file when using -Publish."
    }
    $runArgs = @("run", "--project", $HostProject, "-c", "Release", "--no-build", "--no-launch-profile", "--", "--publish-dacpac", (Resolve-Path $Output).Path, "--connection", $cs)
    if ($ScriptOnly) { $runArgs += "--script-only" }
    if ($AllowDataLoss) { $runArgs += "--allow-data-loss" }
    if (-not [string]::IsNullOrWhiteSpace($ScriptOutput)) { $runArgs += @("--script-output", $ScriptOutput) }
    & dotnet @runArgs
    if ($LASTEXITCODE -ne 0) { throw "Publish/script failed." }
    Write-Host "DACPAC publish finished for $Output"
    return
}

$sqlproj = Resolve-SqlProject $ProjectPath
$runArgs = @("run", "--project", $HostProject, "-c", "Release", "--no-build", "--no-launch-profile", "--", "--build-dacpac", $sqlproj, "--configuration", $Configuration)
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
