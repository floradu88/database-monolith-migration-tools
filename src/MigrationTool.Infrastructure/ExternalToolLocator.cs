using System.Diagnostics;
using MigrationTool.Domain;

namespace MigrationTool.Infrastructure;

public static class ExternalToolLocator
{
    public static string? FindDotnet()
    {
        var fromPath = FindOnPath("dotnet");
        if (fromPath is not null)
        {
            return fromPath;
        }

        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe"),
            @"D:\dotnet\dotnet.exe"
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    public static string? FindMsBuild()
    {
        var fromVsWhere = FindViaVsWhere("**/MSBuild.exe");
        if (fromVsWhere is not null)
        {
            // Prefer amd64 when both appear.
            var amd64 = fromVsWhere.FirstOrDefault(p =>
                p.Contains($"{Path.DirectorySeparatorChar}amd64{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
            return amd64 ?? fromVsWhere.FirstOrDefault();
        }

        return FindOnPath("msbuild");
    }

    public static string? FindSqlPackage()
    {
        var fromPath = FindOnPath("sqlpackage");
        if (fromPath is not null)
        {
            return fromPath;
        }

        var fromVsWhere = FindViaVsWhere("**/SqlPackage.exe");
        return fromVsWhere?.FirstOrDefault();
    }

    public static DacpacPrerequisitesReport GetPrerequisites()
    {
        var dotnet = FindDotnet();
        var msbuild = FindMsBuild();
        var sqlpackage = FindSqlPackage();
        var targetingPack = FindNetFramework472TargetingPack();
        var ssdtTargets = FindSsdtTargets();

        var tools = new List<DacpacToolPrerequisite>
        {
            new(
                "dotnet (.NET SDK)",
                dotnet is not null,
                dotnet,
                "Required for SDK-style Microsoft.Build.Sql projects and MigrationTool.Host. global.json accepts 10.0.203+."),
            new(
                "MSBuild (Visual Studio)",
                msbuild is not null,
                msbuild,
                "Required for classic SSDT .sqlproj. Install VS with SQL Server Data Tools."),
            new(
                "SSDT SqlTasks.targets",
                ssdtTargets is not null,
                ssdtTargets,
                "Shipped with Visual Studio SSDT. Classic builds import these targets."),
            new(
                ".NET Framework 4.7.2 targeting pack",
                targetingPack is not null,
                targetingPack,
                "Classic SSDT builds retarget to v4.7.2."),
            new(
                "SqlPackage",
                sqlpackage is not null,
                sqlpackage,
                "Required for --extract-dacpac. VS SSDT or: dotnet tool install -g microsoft.sqlpackage")
        };

        return new DacpacPrerequisitesReport(
            tools,
            CanBuildSdkStyle: dotnet is not null,
            CanBuildClassicSsdt: msbuild is not null && ssdtTargets is not null && targetingPack is not null,
            CanExtract: sqlpackage is not null);
    }

    private static string? FindNetFramework472TargetingPack()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Reference Assemblies",
            "Microsoft",
            "Framework",
            ".NETFramework",
            "v4.7.2");
        return Directory.Exists(path) ? path : null;
    }

    private static string? FindSsdtTargets()
    {
        var fromVsWhere = FindViaVsWhere("**/Microsoft.Data.Tools.Schema.SqlTasks.targets");
        return fromVsWhere?.FirstOrDefault();
    }

    private static string? FindOnPath(string toolName)
    {
        var extensions = OperatingSystem.IsWindows()
            ? new[] { ".exe", ".cmd", ".bat", "" }
            : new[] { "" };

        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(directory, toolName + extension);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static IReadOnlyList<string>? FindViaVsWhere(string findPattern)
    {
        var vswhere = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Microsoft Visual Studio",
            "Installer",
            "vswhere.exe");
        if (!File.Exists(vswhere))
        {
            return null;
        }

        var start = new ProcessStartInfo
        {
            FileName = vswhere,
            ArgumentList =
            {
                "-latest",
                "-products",
                "*",
                "-find",
                findPattern
            },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(start);
        if (process is null)
        {
            return null;
        }

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(15_000);
        var lines = output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(File.Exists)
            .ToArray();
        return lines.Length == 0 ? null : lines;
    }
}
