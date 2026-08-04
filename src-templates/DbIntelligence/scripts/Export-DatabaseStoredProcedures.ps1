#Requires -Version 5.1
<#
.SYNOPSIS
  Export all user stored procedures from a SQL Server database to a single .sql file.

.DESCRIPTION
  Read-only: queries sys.procedures + sys.sql_modules (OBJECT_DEFINITION equivalent)
  and writes CREATE PROCEDURE scripts (with GO batch separators) to -OutputFile.

  Connection resolution (first wins):
    1. -SqlConnectionString
    2. $env:DbIntelligence__SqlConnectionString
    3. -UseShowcaseLocalDefaults → Showcase LocalDB Owned from kit appsettings.json

  Does not invent production credentials. Never runs destructive SQL.

.PARAMETER OutputFile
  Full file path of the .sql script to write (required). Parent folder is created if missing.

.PARAMETER SqlConnectionString
  SQL Server connection string (database must be set in the string or via Initial Catalog/Database).

.PARAMETER UseShowcaseLocalDefaults
  Infer LocalDB Owned connection from Showcase appsettings.json.

.PARAMETER Endpoint
  Owned (default) or SourceFacade when using Showcase defaults.

.PARAMETER Schema
  Optional schema filter (e.g. showcase). When omitted, all non-ms_shipped procedures are exported.

.PARAMETER IncludeEncrypted
  Include encrypted modules (definition will be NULL — a stub comment is written). Default: skip encrypted.

.PARAMETER ListOnly
  Write only a name list (schema.procedure per line), not full definitions.

.EXAMPLE
  .\Export-DatabaseStoredProcedures.ps1 `
    -OutputFile "D:\exports\ShowcaseOwned-procedures.sql" `
    -UseShowcaseLocalDefaults

.EXAMPLE
  .\Export-DatabaseStoredProcedures.ps1 `
    -OutputFile "C:\temp\monolith-sps.sql" `
    -SqlConnectionString "Server=.;Database=Monolith;Trusted_Connection=True;TrustServerCertificate=True"

.EXAMPLE
  .\Export-DatabaseStoredProcedures.ps1 `
    -OutputFile "D:\exports\sp-list.txt" `
    -SqlConnectionString "Server=.;Database=Monolith;Trusted_Connection=True;TrustServerCertificate=True" `
    -ListOnly
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [Alias("Path", "FilePath", "FullName")]
    [string]$OutputFile,

    [string]$SqlConnectionString = "",

    [switch]$UseShowcaseLocalDefaults,

    [ValidateSet("Owned", "SourceFacade")]
    [string]$Endpoint = "Owned",

    [string]$Schema = "",

    [switch]$IncludeEncrypted,

    [switch]$ListOnly
)

$ErrorActionPreference = "Stop"
$Scripts = $PSScriptRoot
. (Join-Path $Scripts "Resolve-DbIntelligenceSqlConnection.ps1")

$cs = Resolve-DbIntelligenceSqlConnection `
    -SqlConnectionString $SqlConnectionString `
    -UseShowcaseLocalDefaults:$UseShowcaseLocalDefaults `
    -Endpoint $Endpoint

if ([string]::IsNullOrWhiteSpace($cs)) {
    throw @"
No SqlConnectionString resolved.
Provide -SqlConnectionString, set `$env:DbIntelligence__SqlConnectionString, or pass -UseShowcaseLocalDefaults.
"@
}

if ([string]::IsNullOrWhiteSpace($OutputFile)) {
    throw "-OutputFile (full filepath) is required."
}

# Normalize to absolute path (does not require the file to exist yet)
if (-not [System.IO.Path]::IsPathRooted($OutputFile)) {
    $OutputFile = Join-Path (Get-Location).Path $OutputFile
}
$OutputFile = [System.IO.Path]::GetFullPath($OutputFile)

$parent = Split-Path -Parent $OutputFile
if (-not [string]::IsNullOrWhiteSpace($parent) -and -not (Test-Path -LiteralPath $parent)) {
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
}

$query = @"
SELECT
    SCHEMA_NAME(p.schema_id) AS SchemaName,
    p.name AS ProcedureName,
    p.object_id AS ObjectId,
    CASE WHEN m.definition IS NULL THEN 1 ELSE 0 END AS IsEncryptedOrMissing,
    m.definition AS Definition
FROM sys.procedures AS p
LEFT JOIN sys.sql_modules AS m ON m.object_id = p.object_id
WHERE p.is_ms_shipped = 0
ORDER BY SCHEMA_NAME(p.schema_id), p.name;
"@

Write-Host "Connecting (read-only inventory)..." -ForegroundColor Cyan
$safeCs = [regex]::Replace($cs, '(?i)(Password|Pwd)=[^;]*', '$1=***')
Write-Host "Connection (redacted): $safeCs"

# Prefer Microsoft.Data.SqlClient if loaded; else System.Data.SqlClient (Windows PowerShell)
$connection = $null
try {
    $connection = New-Object Microsoft.Data.SqlClient.SqlConnection $cs
}
catch {
    $connection = New-Object System.Data.SqlClient.SqlConnection $cs
}

$rows = New-Object System.Collections.Generic.List[object]
try {
    $connection.Open()
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = $query
    $cmd.CommandTimeout = 120
    $reader = $cmd.ExecuteReader()
    while ($reader.Read()) {
        $schemaName = [string]$reader["SchemaName"]
        if (-not [string]::IsNullOrWhiteSpace($Schema) -and
            -not $schemaName.Equals($Schema, [StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        $isEncrypted = [int]$reader["IsEncryptedOrMissing"] -eq 1
        if ($isEncrypted -and -not $IncludeEncrypted) {
            continue
        }

        $def = if ($reader["Definition"] -is [DBNull]) { $null } else { [string]$reader["Definition"] }
        $rows.Add([pscustomobject]@{
            SchemaName    = $schemaName
            ProcedureName = [string]$reader["ProcedureName"]
            IsEncrypted   = $isEncrypted
            Definition    = $def
        }) | Out-Null
    }
    $reader.Close()
}
finally {
    if ($connection.State -ne [System.Data.ConnectionState]::Closed) {
        $connection.Close()
    }
    $connection.Dispose()
}

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("-- Generated by Export-DatabaseStoredProcedures.ps1")
[void]$sb.AppendLine("-- GeneratedAt: $([DateTimeOffset]::UtcNow.ToString('o'))")
[void]$sb.AppendLine("-- SourceDatabase: (from connection string)")
[void]$sb.AppendLine("-- ProcedureCount: $($rows.Count)")
[void]$sb.AppendLine("-- Read-only export. Review before applying anywhere.")
[void]$sb.AppendLine("SET NOCOUNT ON;")
[void]$sb.AppendLine("GO")
[void]$sb.AppendLine()

if ($ListOnly) {
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine("# Stored procedure list — $($rows.Count) entries")
    [void]$sb.AppendLine("# GeneratedAt: $([DateTimeOffset]::UtcNow.ToString('o'))")
    foreach ($r in $rows) {
        [void]$sb.AppendLine("$($r.SchemaName).$($r.ProcedureName)")
    }
}
else {
    foreach ($r in $rows) {
        $qualified = "[$($r.SchemaName)].[$($r.ProcedureName)]"
        [void]$sb.AppendLine("/* ===== $qualified ===== */")
        if ($r.IsEncrypted -or [string]::IsNullOrWhiteSpace($r.Definition)) {
            [void]$sb.AppendLine("-- Definition unavailable (encrypted or missing module text).")
            [void]$sb.AppendLine("-- EXEC sys.sp_helptext N'$($r.SchemaName).$($r.ProcedureName)';")
            [void]$sb.AppendLine("GO")
            [void]$sb.AppendLine()
            continue
        }

        # OBJECT_DEFINITION / sql_modules.definition is CREATE PROCEDURE ...
        $body = $r.Definition.TrimEnd()
        [void]$sb.AppendLine($body)
        if (-not $body.EndsWith("GO", [StringComparison]::OrdinalIgnoreCase)) {
            [void]$sb.AppendLine("GO")
        }
        [void]$sb.AppendLine()
    }
}

[System.IO.File]::WriteAllText($OutputFile, $sb.ToString(), [System.Text.UTF8Encoding]::new($false))

Write-Host ""
Write-Host "Exported $($rows.Count) stored procedure(s) to:" -ForegroundColor Green
Write-Host "  $OutputFile"
$rows | Select-Object -First 50 SchemaName, ProcedureName | Format-Table -AutoSize
if ($rows.Count -gt 50) {
    Write-Host "... and $($rows.Count - 50) more (see file)."
}

exit 0
