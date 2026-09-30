using System.Diagnostics;
using System.Text;
using MigrationTool.Application;
using MigrationTool.Domain;
using Microsoft.Extensions.Logging;

namespace MigrationTool.Infrastructure;

public sealed class DacpacBuilder : IDacpacBuilder
{
    private readonly ILogger<DacpacBuilder> _logger;

    public DacpacBuilder(ILogger<DacpacBuilder> logger)
    {
        _logger = logger;
    }

    public DacpacPrerequisitesReport GetPrerequisites() => ExternalToolLocator.GetPrerequisites();

    public async Task<DacpacOperationResult> BuildFromSqlProjectAsync(
        DacpacBuildRequest request,
        CancellationToken cancellationToken)
    {
        string projectPath;
        try
        {
            projectPath = SqlProjectPathResolver.Resolve(request.SqlProjectPath);
        }
        catch (Exception ex) when (ex is ArgumentException or FileNotFoundException or InvalidOperationException)
        {
            return Fail(SqlProjectKind.Unknown, "", ex.Message);
        }

        var kind = SqlProjectClassifier.ClassifyFile(projectPath);
        if (kind == SqlProjectKind.Unknown)
        {
            return Fail(kind, "", "Could not classify .sqlproj as Microsoft.Build.Sql SDK-style or classic SSDT.");
        }

        var configuration = string.IsNullOrWhiteSpace(request.Configuration) ? "Debug" : request.Configuration;
        var projectDirectory = Path.GetDirectoryName(projectPath)!;
        var outputDirectory = string.IsNullOrWhiteSpace(request.OutputDirectory)
            ? Path.Combine(projectDirectory, "bin", configuration)
            : Path.GetFullPath(request.OutputDirectory);
        Directory.CreateDirectory(outputDirectory);

        string? intermediateDirectory = null;
        if (!string.IsNullOrWhiteSpace(request.IntermediateDirectory))
        {
            intermediateDirectory = Path.GetFullPath(request.IntermediateDirectory);
            Directory.CreateDirectory(intermediateDirectory);
        }

        var log = new StringBuilder();
        ProcessStartInfo start;
        string toolUsed;

        if (kind == SqlProjectKind.SdkStyle)
        {
            var dotnet = ExternalToolLocator.FindDotnet();
            if (dotnet is null)
            {
                return Fail(kind, log.ToString(), "dotnet was not found on PATH.");
            }

            toolUsed = dotnet;
            start = new ProcessStartInfo
            {
                FileName = dotnet,
                ArgumentList =
                {
                    "build",
                    projectPath,
                    "-c",
                    configuration,
                    $"-p:OutDir={EnsureTrailingSlash(outputDirectory)}"
                },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = projectDirectory
            };
            if (intermediateDirectory is not null)
            {
                start.ArgumentList.Add($"-p:BaseIntermediateOutputPath={EnsureTrailingSlash(intermediateDirectory)}");
                start.ArgumentList.Add(
                    $"-p:IntermediateOutputPath={EnsureTrailingSlash(Path.Combine(intermediateDirectory, configuration))}");
            }

            ApplyTempDirectory(start, intermediateDirectory ?? outputDirectory);
        }
        else
        {
            var msbuild = ExternalToolLocator.FindMsBuild();
            if (msbuild is null)
            {
                return Fail(
                    kind,
                    log.ToString(),
                    "MSBuild with SSDT was not found. Install Visual Studio SQL Server Data Tools, or convert the project to Microsoft.Build.Sql SDK-style.");
            }

            toolUsed = msbuild;
            start = new ProcessStartInfo
            {
                FileName = msbuild,
                ArgumentList =
                {
                    projectPath,
                    $"/p:Configuration={configuration}",
                    $"/p:OutDir={EnsureTrailingSlash(outputDirectory)}",
                    "/p:TargetFrameworkVersion=v4.7.2",
                    "/p:LangVersion=latest",
                    "/p:Nullable=disable",
                    "/v:minimal",
                    "/nologo",
                    "/restore"
                },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = projectDirectory
            };
            if (intermediateDirectory is not null)
            {
                start.ArgumentList.Add($"/p:BaseIntermediateOutputPath={EnsureTrailingSlash(intermediateDirectory)}");
                start.ArgumentList.Add(
                    $"/p:IntermediateOutputPath={EnsureTrailingSlash(Path.Combine(intermediateDirectory, configuration))}");
            }

            ApplyTempDirectory(start, intermediateDirectory ?? outputDirectory);
        }

        var (exitCode, combined) = await RunProcessAsync(start, log, cancellationToken);
        if (exitCode != 0)
        {
            return Fail(kind, combined, $"Build failed with exit code {exitCode} using {toolUsed}.");
        }

        var dacpac = FindDacpac(outputDirectory, Path.GetFileNameWithoutExtension(projectPath));
        if (dacpac is null)
        {
            dacpac = FindDacpac(projectDirectory, Path.GetFileNameWithoutExtension(projectPath));
        }

        if (dacpac is null)
        {
            return Fail(kind, combined, $"Build succeeded but no .dacpac was found under {outputDirectory}.");
        }

        _logger.LogInformation("Built dacpac {Dacpac} from {Project} via {Tool}", dacpac, projectPath, toolUsed);
        return new DacpacOperationResult(true, dacpac, toolUsed, kind, combined, null);
    }

    public async Task<DacpacOperationResult> ExtractFromDatabaseAsync(
        DacpacExtractRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ConnectionString))
        {
            return Fail(SqlProjectKind.Unknown, "", "Connection string is required for SqlPackage Extract.");
        }

        var sqlpackage = ExternalToolLocator.FindSqlPackage();
        if (sqlpackage is null)
        {
            return Fail(
                SqlProjectKind.Unknown,
                "",
                "SqlPackage.exe was not found. Install Visual Studio SSDT / SQL Server Data Tools, or `dotnet tool install -g microsoft.sqlpackage`.");
        }

        var outputPath = Path.GetFullPath(request.OutputDacpacPath);
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        var log = new StringBuilder();
        var start = new ProcessStartInfo
        {
            FileName = sqlpackage,
            ArgumentList =
            {
                "/Action:Extract",
                $"/TargetFile:{outputPath}",
                $"/SourceConnectionString:{request.ConnectionString}"
            },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (!string.IsNullOrWhiteSpace(request.DatabaseName))
        {
            log.AppendLine($"Database name hint: {request.DatabaseName}");
        }

        var (exitCode, combined) = await RunProcessAsync(start, log, cancellationToken);
        if (exitCode != 0)
        {
            return Fail(SqlProjectKind.Unknown, combined, $"SqlPackage Extract failed with exit code {exitCode}.");
        }

        if (!File.Exists(outputPath))
        {
            return Fail(SqlProjectKind.Unknown, combined, $"Extract reported success but file missing: {outputPath}");
        }

        _logger.LogInformation("Extracted dacpac {Dacpac} via SqlPackage", outputPath);
        return new DacpacOperationResult(true, outputPath, sqlpackage, SqlProjectKind.Unknown, combined, null);
    }

    private static void ApplyTempDirectory(ProcessStartInfo start, string preferredRoot)
    {
        try
        {
            var temp = Path.Combine(preferredRoot, ".tmp");
            Directory.CreateDirectory(temp);
            start.Environment["TEMP"] = temp;
            start.Environment["TMP"] = temp;
        }
        catch
        {
            // Keep process defaults when the preferred root is not writable.
        }
    }

    private static async Task<(int ExitCode, string Log)> RunProcessAsync(
        ProcessStartInfo start,
        StringBuilder log,
        CancellationToken cancellationToken)
    {
        log.AppendLine($"> {start.FileName} {string.Join(' ', start.ArgumentList.Select(RedactConnectionStringArg))}");
        using var process = new Process { StartInfo = start, EnableRaisingEvents = true };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stdout.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stderr.AppendLine(e.Data);
            }
        };

        if (!process.Start())
        {
            log.AppendLine("Failed to start process.");
            return (-1, log.ToString());
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(cancellationToken);
        log.Append(stdout);
        if (stderr.Length > 0)
        {
            log.Append(stderr);
        }

        return (process.ExitCode, log.ToString());
    }

    private static string RedactConnectionStringArg(string argument)
    {
        if (argument.StartsWith("/SourceConnectionString:", StringComparison.OrdinalIgnoreCase)
            || argument.StartsWith("/TargetConnectionString:", StringComparison.OrdinalIgnoreCase))
        {
            var colon = argument.IndexOf(':');
            return argument[..(colon + 1)] + "***";
        }

        return argument;
    }

    private static string? FindDacpac(string root, string projectName)
    {
        if (!Directory.Exists(root))
        {
            return null;
        }

        var preferred = Path.Combine(root, projectName + ".dacpac");
        if (File.Exists(preferred))
        {
            return preferred;
        }

        return Directory.EnumerateFiles(root, "*.dacpac", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static string EnsureTrailingSlash(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;

    private static DacpacOperationResult Fail(SqlProjectKind kind, string log, string error) =>
        new(false, null, "", kind, log, error);
}
