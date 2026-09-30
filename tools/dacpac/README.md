# DACPAC builder

Build a `.dacpac` from a SQL Server `.sqlproj`, or extract one from a live database with Visual Studio / .NET DAC tools.

## Install tools (no Visual Studio IDE)

```powershell
cd C:\code\projects\database-monolith-migration-tools\tools\dacpac

# Check what is already present
.\Install-DacpacTools.ps1 -CheckOnly

# Default: verify .NET SDK + install SqlPackage (winget or dotnet tool)
# Enough for SDK-style Microsoft.Build.Sql projects and --extract-dacpac
.\Install-DacpacTools.ps1 -Yes

# Optional: classic SSDT builds — Visual Studio *Build Tools* only (not the IDE)
# VS 2022 Build Tools + SQL Server Data Tools (build) workload:
.\Install-DacpacTools.ps1 -IncludeClassicSsdt -BuildToolsChannel 2022 -Yes

# VS 2026 / latest Build Tools + SSDT build workload:
.\Install-DacpacTools.ps1 -IncludeClassicSsdt -BuildToolsChannel Latest -Yes
```

| Mode | Installs | Does not install |
|------|----------|------------------|
| Default / `-SqlPackageOnly` | SqlPackage (+ requires existing .NET SDK) | Visual Studio IDE |
| `-IncludeClassicSsdt` | VS **Build Tools** + `DataBuildTools` (SSDT build) for 2022 or 2026 | Community / Professional / Enterprise IDE |

Winget package ids used:

- `Microsoft.SqlPackage`
- `Microsoft.VisualStudio.2022.BuildTools` (`-BuildToolsChannel 2022`)
- `Microsoft.VisualStudio.BuildTools` (`2026` / `Latest`)

Build Tools installs usually need an **elevated** PowerShell. SqlPackage via `dotnet tool` does not.

## Prerequisites

Check what this machine has:

```powershell
dotnet run --project ..\..\src\MigrationTool.Host -- --dacpac-prereqs
# or
.\Invoke-DacpacReady.ps1 -CheckPrereqs
```

| Need | Used for | How to get it |
|------|----------|---------------|
| .NET 10 SDK (`10.0.203+`) | SDK-style `Microsoft.Build.Sql` builds + MigrationTool.Host | Install .NET 10 SDK; `global.json` rolls forward on the 10.0 line |
| NuGet (`Microsoft.Build.Sql` **2.3.0**) | SDK-style projects | Restored automatically; pinned in root `global.json` `msbuild-sdks` |
| Visual Studio + **SSDT** + MSBuild | Classic SSDT `.sqlproj` (e.g. Showcase) | VS workload “SQL Server Data Tools” |
| .NET Framework **4.7.2** targeting pack | Classic SSDT retarget | Visual Studio installer / Developer Pack |
| **SqlPackage** | `--extract-dacpac` / `-Extract` | VS SSDT, or `dotnet tool install -g microsoft.sqlpackage` |
| Disk space | SSDT model + NuGet | Prefer `-Output` / `-Intermediate` on a roomy drive (e.g. `D:`) if `C:` is tight |

**Minimal path (fixture):** .NET 10 SDK only.

Extract also needs a SQL Server connection string (`MIGRATION_CONNECTION_STRING` or `-ConnectionString`). PostgreSQL is not supported for dacpac.

## Ready (path only)

```powershell
cd C:\code\projects\database-monolith-migration-tools\tools\dacpac

.\Invoke-DacpacReady.ps1 "C:\path\to\Your.Database.sqlproj"
.\Invoke-DacpacReady.ps1 ".\fixtures\DacpacFixture" -Output "D:\dacpac-out" -Intermediate "D:\dacpac-obj"
```

## MigrationTool Host CLI

```powershell
cd C:\code\projects\database-monolith-migration-tools

dotnet run --project src/MigrationTool.Host -- --dacpac-prereqs

dotnet run --project src/MigrationTool.Host -- --build-dacpac path\to\project.sqlproj --configuration Debug --output D:\dacpac-out

# Extract (does not apply schema). Prefer MIGRATION_CONNECTION_STRING.
dotnet run --project src/MigrationTool.Host -- --extract-dacpac D:\out\live.dacpac
```

`--build-dacpac` accepts a `.sqlproj` file or a folder that contains exactly one `.sqlproj`.

## What it uses

| Project kind | Tool |
|--------------|------|
| `Sdk="Microsoft.Build.Sql"` | `dotnet build` (SDK version pinned in root `global.json` → `msbuild-sdks`) |
| Classic SSDT (`SqlTasks.targets`) | Visual Studio `MSBuild.exe` via `vswhere` |
| Live database extract | `SqlPackage.exe` `/Action:Extract` (VS SSDT or `dotnet tool install -g microsoft.sqlpackage`) |

Classic builds pass `/p:TargetFrameworkVersion=v4.7.2`, `/p:LangVersion=latest`, and `/p:Nullable=disable` so modern VS MSBuild can compile SSDT projects.

## Fixture

- `fixtures/DacpacFixture` — tiny **SDK-style** SQL project for smoke builds
- `fixtures/ClassicSsdtFixture` — tiny **classic SSDT** SQL project (needs VS MSBuild + SSDT)

Kit Showcase `.sqlproj` scripts often use `CREATE OR ALTER` and `GO`, which SSDT rejects with `SQL70001` until rewritten as SSDT Build objects (`CREATE PROCEDURE` / `CREATE TABLE` without batch separators). Prefer the fixtures to verify tooling.

## Safety

- Build and extract only. This tool does **not** publish/deploy a dacpac.
- Do not point Extract at production without DBA approval.
- Do not commit connection strings.
