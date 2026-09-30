#Requires -Version 5.1
<#
.SYNOPSIS
  Install DACPAC tooling without installing the Visual Studio IDE.

.DESCRIPTION
  Installs the tools this kit uses to build/extract .dacpac files:

  Always (default):
    - Verifies a .NET SDK is available (needed for SDK-style Microsoft.Build.Sql + MigrationTool)
    - Installs SqlPackage via winget (preferred) or `dotnet tool install -g microsoft.sqlpackage`

  Optional classic SSDT:
    - Installs Visual Studio *Build Tools* only (not Community / Professional / Enterprise)
      with the SQL Server Data Tools *build* workload, for VS 2022 or VS 2026 channels.

  Does NOT install Visual Studio IDE. Does NOT publish dacpacs to databases.

.PARAMETER SqlPackageOnly
  Install/verify SqlPackage + .NET SDK only (default when -IncludeClassicSsdt is omitted).

.PARAMETER IncludeClassicSsdt
  Also install VS Build Tools + DataBuildTools (SSDT build) for classic .sqlproj.

.PARAMETER BuildToolsChannel
  Which Build Tools channel to install when -IncludeClassicSsdt is set:
    2022   -> Microsoft.VisualStudio.2022.BuildTools
    2026   -> Microsoft.VisualStudio.BuildTools (VS 18 / 2026)
    Latest -> same as 2026

.PARAMETER SkipSqlPackage
  Do not install SqlPackage (useful if you only need classic MSBuild/SSDT).

.PARAMETER CheckOnly
  Report what is installed / missing; install nothing.

.PARAMETER Yes
  Non-interactive; accept winget / installer prompts where supported.

.PARAMETER WhatIf
  Show planned actions without installing.

.EXAMPLE
  .\Install-DacpacTools.ps1 -Yes
  # .NET SDK check + SqlPackage (enough for SDK-style .sqlproj + extract)

.EXAMPLE
  .\Install-DacpacTools.ps1 -IncludeClassicSsdt -BuildToolsChannel 2022 -Yes
  # Also install VS 2022 Build Tools + SSDT build workload (no IDE)

.EXAMPLE
  .\Install-DacpacTools.ps1 -IncludeClassicSsdt -BuildToolsChannel Latest -Yes
  # VS 2026 Build Tools + SSDT build workload

.EXAMPLE
  .\Install-DacpacTools.ps1 -CheckOnly
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [switch]$SqlPackageOnly,

    [switch]$IncludeClassicSsdt,

    [ValidateSet("2022", "2026", "Latest")]
    [string]$BuildToolsChannel = "Latest",

    [switch]$SkipSqlPackage,

    [switch]$CheckOnly,

    [switch]$Yes
)

$ErrorActionPreference = "Stop"
$Here = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Resolve-Path (Join-Path $Here "..\..")
$HostProject = Join-Path $RepoRoot "src\MigrationTool.Host\MigrationTool.Host.csproj"

function Write-Step([string]$Message) {
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Test-IsAdmin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Find-DotNet {
    $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    foreach ($candidate in @(
            (Join-Path ${env:ProgramFiles} "dotnet\dotnet.exe"),
            "D:\dotnet\dotnet.exe"
        )) {
        if (Test-Path -LiteralPath $candidate) { return $candidate }
    }
    return $null
}

function Find-Winget {
    $cmd = Get-Command winget -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $appPaths = @(
        (Join-Path $env:LOCALAPPDATA "Microsoft\WindowsApps\winget.exe"),
        (Join-Path ${env:ProgramFiles} "WindowsApps\Microsoft.DesktopAppInstaller*\winget.exe")
    )
    foreach ($p in $appPaths) {
        $resolved = Get-Item $p -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($resolved) { return $resolved.FullName }
    }
    return $null
}

function Find-SqlPackage {
    $cmd = Get-Command sqlpackage -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $dotnetTools = Join-Path $env:USERPROFILE ".dotnet\tools\sqlpackage.exe"
    if (Test-Path -LiteralPath $dotnetTools) { return $dotnetTools }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path -LiteralPath $vswhere) {
        $found = & $vswhere -latest -products * -find "**/SqlPackage.exe" 2>$null
        if ($found) {
            $line = @($found | Where-Object { $_ -and (Test-Path -LiteralPath $_) }) | Select-Object -First 1
            if ($line) { return $line }
        }
    }
    return $null
}

function Get-BuildToolsWingetId([string]$Channel) {
    switch ($Channel) {
        "2022" { return "Microsoft.VisualStudio.2022.BuildTools" }
        "2026" { return "Microsoft.VisualStudio.BuildTools" }
        "Latest" { return "Microsoft.VisualStudio.BuildTools" }
        default { throw "Unknown BuildToolsChannel: $Channel" }
    }
}

function Install-SqlPackageWithWinget([string]$Winget, [bool]$Accept) {
    $args = @(
        "install", "--id", "Microsoft.SqlPackage", "-e", "--source", "winget",
        "--accept-package-agreements", "--accept-source-agreements"
    )
    if ($Accept) { $args += "--disable-interactivity" }
    Write-Host "winget $($args -join ' ')"
    & $Winget @args
    return $LASTEXITCODE
}

function Install-SqlPackageWithDotNet([string]$DotNet) {
    Write-Host "$DotNet tool install -g microsoft.sqlpackage"
    & $DotNet tool update -g microsoft.sqlpackage 2>$null
    if ($LASTEXITCODE -ne 0) {
        & $DotNet tool install -g microsoft.sqlpackage
    }
    return $LASTEXITCODE
}

function Install-BuildToolsSsdt([string]$Winget, [string]$PackageId, [bool]$Accept) {
    # Build Tools only — workload is SQL Server Data Tools (build), not the VS IDE.
    # Component docs: Microsoft.VisualStudio.Workload.DataBuildTools
    $override = @(
        "--wait",
        "--norestart",
        "--add", "Microsoft.VisualStudio.Workload.DataBuildTools",
        "--includeRecommended"
    ) -join " "
    if ($Accept) {
        $override = "--passive $override"
    }
    else {
        $override = "--passive $override"
    }

    $args = @(
        "install", "--id", $PackageId, "-e", "--source", "winget",
        "--accept-package-agreements", "--accept-source-agreements",
        "--disable-interactivity",
        "--override", $override
    )
    Write-Host "winget $($args -join ' ')"
    Write-Host "Override: $override"
    & $Winget @args
    return $LASTEXITCODE
}

function Show-LocalStatus {
    Write-Step "Current tool status"
    $dotnet = Find-DotNet
    $sqlpackage = Find-SqlPackage
    $winget = Find-Winget
    Write-Host ("  dotnet:     " + $(if ($dotnet) { $dotnet } else { "MISSING" }))
    Write-Host ("  SqlPackage: " + $(if ($sqlpackage) { $sqlpackage } else { "MISSING" }))
    Write-Host ("  winget:     " + $(if ($winget) { $winget } else { "MISSING" }))
    Write-Host ("  admin:      " + $(if (Test-IsAdmin) { "yes" } else { "no (Build Tools install usually needs elevation)" }))

    if ((Test-Path -LiteralPath $HostProject) -and $dotnet) {
        Write-Host ""
        Write-Host "  MigrationTool prereq report:"
        & $dotnet run --project $HostProject -c Release --no-launch-profile -- --dacpac-prereqs
    }
}

# --- main ---

Write-Host "DACPAC tools installer (no Visual Studio IDE)"
Write-Host "Repo: $RepoRoot"

if ($CheckOnly) {
    Show-LocalStatus
    exit 0
}

$dotnet = Find-DotNet
if (-not $dotnet) {
    throw @"
.NET SDK not found. Install .NET 10 SDK (10.0.203+), then re-run.
  https://dotnet.microsoft.com/download
SDK-style dacpac builds and MigrationTool.Host require dotnet.
"@
}
Write-Step "Using dotnet: $dotnet"
& $dotnet --list-sdks

$installSqlPackage = -not $SkipSqlPackage
if ($SqlPackageOnly -and $IncludeClassicSsdt) {
    Write-Warning "Both -SqlPackageOnly and -IncludeClassicSsdt were set; installing SqlPackage and classic Build Tools."
}

if ($installSqlPackage) {
    Write-Step "SqlPackage"
    $existing = Find-SqlPackage
    if ($existing) {
        Write-Host "Already present: $existing"
    }
    elseif ($WhatIfPreference) {
        Write-Host "WhatIf: would install Microsoft.SqlPackage (winget) or microsoft.sqlpackage (dotnet tool)"
    }
    else {
        $winget = Find-Winget
        $code = 1
        if ($winget) {
            if ($PSCmdlet.ShouldProcess("Microsoft.SqlPackage", "Install via winget")) {
                $code = Install-SqlPackageWithWinget -Winget $winget -Accept:$Yes
            }
        }
        if ($code -ne 0) {
            Write-Host "Falling back to: dotnet tool install -g microsoft.sqlpackage"
            if ($PSCmdlet.ShouldProcess("microsoft.sqlpackage", "Install global .NET tool")) {
                $code = Install-SqlPackageWithDotNet -DotNet $dotnet
            }
        }
        if ($code -ne 0) {
            throw "SqlPackage install failed (exit $code)."
        }

        $toolsPath = Join-Path $env:USERPROFILE ".dotnet\tools"
        if (($env:PATH -split ';') -notcontains $toolsPath) {
            $env:PATH = "$toolsPath;$env:PATH"
            Write-Host "Added $toolsPath to PATH for this session. Add it permanently if sqlpackage is not found later."
        }

        $after = Find-SqlPackage
        if (-not $after) {
            Write-Warning "SqlPackage install finished but sqlpackage.exe was not found on PATH yet. Open a new shell and re-check."
        }
        else {
            Write-Host "SqlPackage ready: $after"
        }
    }
}

if ($IncludeClassicSsdt) {
    Write-Step "Classic SSDT via Visual Studio Build Tools ($BuildToolsChannel) — IDE not installed"
    if (-not (Test-IsAdmin)) {
        Write-Warning "Build Tools install typically requires an elevated PowerShell. Re-run as Administrator if winget fails."
    }

    $winget = Find-Winget
    if (-not $winget) {
        throw "winget not found. Install App Installer from Microsoft Store, or install Build Tools manually from https://visualstudio.microsoft.com/downloads/ (Build Tools only)."
    }

    $packageId = Get-BuildToolsWingetId -Channel $BuildToolsChannel
    Write-Host "Package: $packageId"
    Write-Host "Workload: Microsoft.VisualStudio.Workload.DataBuildTools (SQL Server Data Tools - Build)"
    Write-Host "This is Build Tools only — not Visual Studio Community/Professional/Enterprise."

    if ($WhatIfPreference) {
        Write-Host "WhatIf: would winget install $packageId with DataBuildTools workload"
    }
    elseif ($PSCmdlet.ShouldProcess($packageId, "Install VS Build Tools + SSDT build workload")) {
        if (-not $Yes) {
            $answer = Read-Host "Proceed with Build Tools install? (y/N)"
            if ($answer -notmatch '^[Yy]') {
                Write-Host "Skipped classic SSDT Build Tools install."
                Show-LocalStatus
                exit 0
            }
        }
        $code = Install-BuildToolsSsdt -Winget $winget -PackageId $packageId -Accept:$Yes
        if ($code -ne 0) {
            throw @"
Build Tools install failed (exit $code).
Manual option:
  1. Download Build Tools for Visual Studio 2022 or 2026 (not the full IDE).
  2. Run the installer and select workload 'SQL Server Data Tools - Build'
     (Microsoft.VisualStudio.Workload.DataBuildTools).
  3. Re-run: .\Install-DacpacTools.ps1 -CheckOnly
"@
        }
    }
}

Write-Step "Verify"
Show-LocalStatus

Write-Host ""
Write-Host "Next:" -ForegroundColor Green
Write-Host "  .\Invoke-DacpacReady.ps1 -CheckPrereqs"
Write-Host "  .\Invoke-DacpacReady.ps1 .\fixtures\DacpacFixture -Output D:\dacpac-out"
if ($IncludeClassicSsdt) {
    Write-Host "  .\Invoke-DacpacReady.ps1 .\fixtures\ClassicSsdtFixture -Output D:\dacpac-classic-out"
}
Write-Host ""
Write-Host "SDK-style projects need: .NET SDK + NuGet Microsoft.Build.Sql (auto) + optional SqlPackage for extract."
Write-Host "Classic SSDT projects need: VS Build Tools + DataBuildTools (installed when -IncludeClassicSsdt is used)."
