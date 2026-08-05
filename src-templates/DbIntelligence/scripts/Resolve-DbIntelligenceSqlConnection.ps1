#Requires -Version 5.1
<#
.SYNOPSIS
  Resolve a SQL Server connection string for DbIntelligence SqlScanner (read-only SP inventory).

.DESCRIPTION
  Dot-source this file, then call Resolve-DbIntelligenceSqlConnection / Get-ShowcaseProcedurePlaceholders.

  Does not invent production credentials. Prefer an explicit -SqlConnectionString.
  Optional -UseShowcaseLocalDefaults reads non-secret local placeholders from Showcase
  ShowcaseDataService.Api/appsettings.json (LocalDB), which the kit already ships.

  Cloud appsettings (Azure/Aws) keep CHANGE_ME / empty Password — those must be
  supplied by the operator (user-secrets / env), never committed.
#>

function Resolve-DbIntelligenceSqlConnection {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [string]$SqlConnectionString = "",
        [switch]$UseShowcaseLocalDefaults,
        [ValidateSet("Owned", "SourceFacade")]
        [string]$Endpoint = "Owned",
        [string]$ShowcaseAppsettingsPath = ""
    )

    if (-not [string]::IsNullOrWhiteSpace($SqlConnectionString)) {
        return $SqlConnectionString.Trim()
    }

    $envCs = $env:DbIntelligence__SqlConnectionString
    if (-not [string]::IsNullOrWhiteSpace($envCs)) {
        return $envCs.Trim()
    }

    if (-not $UseShowcaseLocalDefaults) {
        return ""
    }

    if ([string]::IsNullOrWhiteSpace($ShowcaseAppsettingsPath)) {
        $scriptsDir = $PSScriptRoot
        $ShowcaseAppsettingsPath = Join-Path $scriptsDir `
            "..\..\DataServices\ShowcaseDataService\ShowcaseDataService.Api\appsettings.json"
    }

    if (-not (Test-Path -LiteralPath $ShowcaseAppsettingsPath)) {
        throw "Showcase appsettings not found at '$ShowcaseAppsettingsPath'. Pass -SqlConnectionString explicitly."
    }

    $json = Get-Content -LiteralPath $ShowcaseAppsettingsPath -Raw | ConvertFrom-Json
    $endpointNode = $json.Database.$Endpoint
    if ($null -eq $endpointNode) {
        throw "Database.$Endpoint missing in '$ShowcaseAppsettingsPath'."
    }

    $cs = [string]$endpointNode.ConnectionString
    if ([string]::IsNullOrWhiteSpace($cs)) {
        throw "Database.$Endpoint.ConnectionString is empty in '$ShowcaseAppsettingsPath'. Fill it or pass -SqlConnectionString."
    }

    if ($cs -match 'CHANGE_ME') {
        throw "Resolved connection still has CHANGE_ME placeholders. Supply a real non-prod connection via -SqlConnectionString or user-secrets."
    }

    Write-Verbose "Inferred $Endpoint connection from $ShowcaseAppsettingsPath"
    return $cs.Trim()
}

function Get-ShowcaseProcedurePlaceholders {
    <#
    .SYNOPSIS
      Infer Showcase SP name-template placeholders from Domain enums / Infrastructure constants.
    #>
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param()

    $scriptsDir = $PSScriptRoot
    $tokensPath = Join-Path $scriptsDir `
        "..\..\DataServices\ShowcaseDataService\ShowcaseDataService.Domain\ShowcaseProcedureTokens.cs"
    $namesPath = Join-Path $scriptsDir `
        "..\..\DataServices\ShowcaseDataService\ShowcaseDataService.Infrastructure\StoredProcedures\ShowcaseProcedureNames.cs"
    $appsettingsPath = Join-Path $scriptsDir `
        "..\..\DataServices\ShowcaseDataService\ShowcaseDataService.Api\appsettings.json"

    $template = "usp_Showcase_{ShowcaseReportArea}_{ShowcaseReportAction}"
    if (Test-Path -LiteralPath $namesPath) {
        $namesText = Get-Content -LiteralPath $namesPath -Raw
        if ($namesText -match 'ReportTemplate\s*=\s*"([^"]+)"') {
            $template = $Matches[1]
        }
    }

    $areas = @("Sales", "Inventory")
    $actions = @("Summary", "Detail")
    if (Test-Path -LiteralPath $tokensPath) {
        $tokensText = Get-Content -LiteralPath $tokensPath -Raw
        $areaBlock = [regex]::Match($tokensText, 'enum\s+ShowcaseReportArea\s*\{(?<body>[^}]+)\}')
        if ($areaBlock.Success) {
            $areas = @(
                [regex]::Matches($areaBlock.Groups["body"].Value, '(?m)^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=') |
                    ForEach-Object { $_.Groups[1].Value }
            )
        }
        $actionBlock = [regex]::Match($tokensText, 'enum\s+ShowcaseReportAction\s*\{(?<body>[^}]+)\}')
        if ($actionBlock.Success) {
            $actions = @(
                [regex]::Matches($actionBlock.Groups["body"].Value, '(?m)^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=') |
                    ForEach-Object { $_.Groups[1].Value }
            )
        }
    }

    $schema = "showcase"
    $ownedCs = ""
    $sourceCs = ""
    if (Test-Path -LiteralPath $appsettingsPath) {
        $json = Get-Content -LiteralPath $appsettingsPath -Raw | ConvertFrom-Json
        if ($json.Database.Schema) { $schema = [string]$json.Database.Schema }
        try { $ownedCs = Resolve-DbIntelligenceSqlConnection -UseShowcaseLocalDefaults -Endpoint Owned } catch { $ownedCs = "" }
        try { $sourceCs = Resolve-DbIntelligenceSqlConnection -UseShowcaseLocalDefaults -Endpoint SourceFacade } catch { $sourceCs = "" }
    }

    $resolved = foreach ($a in $areas) {
        foreach ($b in $actions) {
            ($template -replace '\{ShowcaseReportArea\}', $a -replace '\{ShowcaseReportAction\}', $b)
        }
    }

    [pscustomobject]@{
        Schema                       = $schema
        NameTemplate                 = $template
        ShowcaseReportArea           = @($areas)
        ShowcaseReportAction         = @($actions)
        ResolvedNames                = @($resolved)
        OwnedConnectionString        = $ownedCs
        SourceFacadeConnectionString = $sourceCs
    }
}
