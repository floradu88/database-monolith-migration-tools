using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MigrationTool.Application;
using MigrationTool.Domain;

namespace MigrationTool.Infrastructure;

public sealed class FileScriptStore : IScriptStore
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly string _inbox;
    private readonly string _success;
    private readonly string _failed;

    public FileScriptStore(IOptions<MigrationOptions> options, IHostEnvironment environment)
    {
        _inbox = Resolve(environment.ContentRootPath, options.Value.InboxPath);
        _success = Resolve(environment.ContentRootPath, options.Value.SuccessPath);
        _failed = Resolve(environment.ContentRootPath, options.Value.FailedPath);
        Directory.CreateDirectory(_inbox);
        Directory.CreateDirectory(_success);
        Directory.CreateDirectory(_failed);
    }

    public IReadOnlyList<InboxScript> ListInbox()
    {
        return Directory.EnumerateFiles(_inbox, "*.sql", SearchOption.AllDirectories)
            .Where(path => !IsUnder(path, _success) && !IsUnder(path, _failed))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                var relative = Path.GetRelativePath(_inbox, path).Replace('\\', '/');
                return new InboxScript(path, relative, Path.GetFileName(path), File.ReadAllText(path));
            })
            .ToList();
    }

    public void SaveContents(string fullPath, string contents) => File.WriteAllText(fullPath, contents, Utf8);

    public void MoveToSuccess(InboxScript script)
    {
        var destination = UniquePath(Path.Combine(_success, script.FileName));
        File.Move(script.FullPath, destination);
    }

    public IReadOnlyList<string> ListFailedSqlFileNames()
    {
        return Directory.EnumerateFiles(_failed, "*.sql", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Cast<string>()
            .ToList();
    }

    public void WriteFailure(string fileName, string contents, int attempt, string errorText, string? diagnostics)
    {
        File.WriteAllText(Path.Combine(_failed, RetryNames.FailureScriptName(attempt, fileName)), contents, Utf8);
        File.WriteAllText(Path.Combine(_failed, RetryNames.FailureErrorName(attempt, fileName)), errorText, Utf8);
        if (!string.IsNullOrWhiteSpace(diagnostics))
        {
            File.WriteAllText(Path.Combine(_failed, RetryNames.FailureDiagnosticsName(attempt, fileName)), diagnostics, Utf8);
        }
    }

    public void RemoveFromInbox(InboxScript script)
    {
        if (File.Exists(script.FullPath))
        {
            File.Delete(script.FullPath);
        }
    }

    public void WriteQueryResult(string fileName, string resultText, DateTimeOffset utc)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var name = $"{stem}_{utc:yyyyMMddHHmmssfff}.result.txt";
        File.WriteAllText(Path.Combine(_success, name), resultText, Utf8);
    }

    public void WritePreStep(string fileName, string sql, string? diagnostics)
    {
        File.WriteAllText(Path.Combine(_success, fileName), sql, Utf8);
        if (!string.IsNullOrWhiteSpace(diagnostics))
        {
            var diagnosticsName = Path.GetFileNameWithoutExtension(fileName) + ".diagnostics.txt";
            File.WriteAllText(Path.Combine(_success, diagnosticsName), diagnostics, Utf8);
        }
    }

    private static string Resolve(string contentRoot, string path) =>
        Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(contentRoot, path));

    private static bool IsUnder(string path, string directory)
    {
        var fullPath = Path.GetFullPath(path);
        var fullDirectory = Path.GetFullPath(directory);
        return fullPath.StartsWith(fullDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || fullPath.Equals(fullDirectory, StringComparison.OrdinalIgnoreCase);
    }

    private static string UniquePath(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path) ?? "";
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        for (var i = 1; i < 1000; i++)
        {
            var candidate = Path.Combine(directory, $"{name}_{i}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException($"Could not find a free file name for '{path}'.");
    }
}
