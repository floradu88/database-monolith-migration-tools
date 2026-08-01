#Requires -Version 5.1
<#
.SYNOPSIS
  Ensure Node.js + npm are available in this PowerShell session via user-scoped fnm (no admin).

.DESCRIPTION
  Prefer Fast Node Manager (fnm) installed for the current user. Does not require elevation.
  Dot-source to mutate PATH in the caller; or invoke with -Install to provision fnm + Node LTS.

.PARAMETER Install
  If node/npm (or fnm) are missing, install Schniz.fnm via winget --scope user and Node LTS via fnm.

.PARAMETER Yes
  Auto-confirm install steps (non-interactive). With -Install, always proceeds.

.PARAMETER NodeVersion
  fnm version alias to install/use (default: lts-latest).

.PARAMETER Quiet
  Reduce informational output.

.EXAMPLE
  # Activate fnm Node in the current session (no install)
  . .\Initialize-DbIntelligenceNode.ps1

  # Provision user-scoped Node/npm if missing
  .\Initialize-DbIntelligenceNode.ps1 -Install -Yes
#>
[CmdletBinding()]
param(
    [switch]$Install,
    [Alias("AssumeYes")]
    [switch]$Yes,
    [string]$NodeVersion = "lts-latest",
    [switch]$Quiet
)

$ErrorActionPreference = "Stop"
# Never `exit` when dot-sourced — that would kill the caller's shell.
$script:IsDotSourced = ($MyInvocation.InvocationName -eq '.')

function Complete-DbIntelligenceNode([bool]$Ok, [int]$ExitCode) {
    if ($script:IsDotSourced) {
        return $Ok
    }
    exit $ExitCode
}

function Write-NodeInfo([string]$Message, [string]$Color = "Cyan") {
    if (-not $Quiet) {
        Write-Host $Message -ForegroundColor $Color
    }
}

function Update-DbIntelligenceSessionPath {
    $user = [System.Environment]::GetEnvironmentVariable("Path", "User")
    $machine = [System.Environment]::GetEnvironmentVariable("Path", "Machine")
    $merged = @($user, $machine) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    $env:Path = ($merged -join ";")

    $wingetLinks = Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Links"
    if ((Test-Path $wingetLinks) -and ($env:Path -notlike "*$wingetLinks*")) {
        $env:Path = "$wingetLinks;$env:Path"
    }
}

function Test-DbIntelligenceCommand([string]$Name) {
    return [bool](Get-Command $Name -ErrorAction SilentlyContinue)
}

function Enable-DbIntelligenceFnm {
    if (-not (Test-DbIntelligenceCommand "fnm")) {
        return $false
    }

    try {
        # Whole script (not line-by-line) — fnm emits multi-line functions.
        Invoke-Expression (& fnm env --use-on-cd --shell powershell | Out-String)
        return $true
    }
    catch {
        Write-Warning "fnm env activation failed: $($_.Exception.Message)"
        return $false
    }
}

function Install-DbIntelligenceFnm {
    if (-not (Test-DbIntelligenceCommand "winget")) {
        throw "winget is required to install fnm without admin. Install App Installer from the Microsoft Store, or install Node.js manually for your user."
    }

    Write-NodeInfo "Installing fnm (user scope, no admin) via winget..."
    & winget install Schniz.fnm --scope user --accept-package-agreements --accept-source-agreements
    if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne -1978335189) {
        # -1978335189 = already installed (winget)
        throw "winget failed to install Schniz.fnm (exit $LASTEXITCODE)."
    }

    Update-DbIntelligenceSessionPath
    if (-not (Test-DbIntelligenceCommand "fnm")) {
        throw "fnm installed but not on PATH. Open a new PowerShell window and re-run."
    }
}

function Install-DbIntelligenceNodeViaFnm([string]$Version) {
    if (-not (Test-DbIntelligenceCommand "fnm")) {
        throw "fnm is not available."
    }

    Write-NodeInfo "Installing Node $Version via fnm (user profile)..."
    & fnm install $Version
    if ($LASTEXITCODE -ne 0) {
        throw "fnm install $Version failed (exit $LASTEXITCODE)."
    }

    & fnm default $Version
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "fnm default $Version returned exit $LASTEXITCODE (continuing)."
    }

    if (-not (Enable-DbIntelligenceFnm)) {
        throw "Node installed but fnm env could not activate in this session."
    }
}

function Confirm-DbIntelligenceNodeInstall([string]$Question) {
    if ($Yes -or $Install) {
        Write-NodeInfo "$Question [Y] (-Yes/-Install)"
        return $true
    }

    $answer = Read-Host "$Question [y/N]"
    return $answer -match '^(y|yes)$'
}

# --- main ---
Update-DbIntelligenceSessionPath
$null = Enable-DbIntelligenceFnm

$nodeOk = Test-DbIntelligenceCommand "node"
$npmOk = Test-DbIntelligenceCommand "npm"
$fnmOk = Test-DbIntelligenceCommand "fnm"

if ($nodeOk -and $npmOk) {
    $nodeVer = (& node -v 2>$null)
    $npmVer = (& npm -v 2>$null)
    Write-NodeInfo "Node/npm ready: node $nodeVer / npm $npmVer ($(if ($fnmOk) { 'fnm' } else { 'PATH' }))" "Green"
    return (Complete-DbIntelligenceNode -Ok $true -ExitCode 0)
}

if (-not $Install) {
    $hint = "Run: .\Initialize-DbIntelligenceNode.ps1 -Install -Yes   (user-scoped fnm Node, no admin)"
    if ($Quiet) {
        Write-Warning "node/npm missing. $hint"
    }
    else {
        Write-Host "node/npm not found on PATH." -ForegroundColor Yellow
        Write-Host "  $hint" -ForegroundColor Yellow
    }
    return (Complete-DbIntelligenceNode -Ok $false -ExitCode 1)
}

if (-not $fnmOk) {
    if (-not (Confirm-DbIntelligenceNodeInstall "Install fnm for this user (winget --scope user, no admin)?")) {
        throw "Skipped fnm install. Node/npm remain unavailable."
    }
    Install-DbIntelligenceFnm
    $fnmOk = $true
}

if (-not (Test-DbIntelligenceCommand "node") -or -not (Test-DbIntelligenceCommand "npm")) {
    if (-not (Confirm-DbIntelligenceNodeInstall "Install Node.js ($NodeVersion) via fnm into your user profile?")) {
        throw "Skipped Node install. node/npm remain unavailable."
    }
    Install-DbIntelligenceNodeViaFnm -Version $NodeVersion
}

$nodeOk = Test-DbIntelligenceCommand "node"
$npmOk = Test-DbIntelligenceCommand "npm"
if (-not ($nodeOk -and $npmOk)) {
    throw "node/npm still missing after install. Open a new terminal and re-run."
}

$nodeVer = (& node -v 2>$null)
$npmVer = (& npm -v 2>$null)
Write-NodeInfo "Node/npm ready (user-scoped): node $nodeVer / npm $npmVer" "Green"
return (Complete-DbIntelligenceNode -Ok $true -ExitCode 0)
