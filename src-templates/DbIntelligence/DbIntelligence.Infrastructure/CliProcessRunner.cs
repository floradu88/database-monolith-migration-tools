using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace DbIntelligence.Infrastructure;

public sealed class CliProcessRunner
{
    public async Task<CliResult> RunAsync(
        string executable,
        IEnumerable<string> arguments,
        string? workingDirectory = null,
        int timeoutSeconds = 300,
        CancellationToken cancellationToken = default)
    {
        var args = arguments.ToList();
        var psi = new ProcessStartInfo
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory
        };

        // On Windows, npm/global shims are often *.cmd; CreateProcess won't resolve them
        // unless launched through cmd.exe.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && NeedsCmdShim(executable))
        {
            psi.FileName = "cmd.exe";
            psi.ArgumentList.Add("/d");
            psi.ArgumentList.Add("/s");
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add(BuildCmdLine(executable, args));
        }
        else
        {
            psi.FileName = executable;
            foreach (var arg in args)
                psi.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                stdout.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                stderr.AppendLine(e.Data);
        };

        try
        {
            if (!process.Start())
                return new CliResult(false, -1, string.Empty, $"Failed to start process '{executable}'.");
        }
        catch (Exception ex)
        {
            return new CliResult(false, -1, string.Empty, $"Failed to start '{executable}': {ex.Message}");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
            return new CliResult(false, -1, stdout.ToString(), $"Timed out after {timeoutSeconds}s. {stderr}");
        }

        return new CliResult(process.ExitCode == 0, process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private static bool NeedsCmdShim(string executable)
    {
        if (executable.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
            executable.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
            return true;

        // Bare command names (codegraph, npm, winget) are commonly cmd shims on Windows.
        return !executable.Contains(Path.DirectorySeparatorChar) &&
               !executable.Contains(Path.AltDirectorySeparatorChar) &&
               !executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildCmdLine(string executable, IReadOnlyList<string> args)
    {
        static string Q(string value) =>
            value.IndexOfAny([' ', '\t', '"']) >= 0
                ? "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\""
                : value;

        return string.Join(' ', new[] { Q(executable) }.Concat(args.Select(Q)));
    }
}

public sealed record CliResult(bool Succeeded, int ExitCode, string StandardOutput, string StandardError);
