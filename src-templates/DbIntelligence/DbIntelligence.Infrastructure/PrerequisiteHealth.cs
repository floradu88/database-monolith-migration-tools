using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using DbIntelligence.Contracts;
using Microsoft.Extensions.Options;

namespace DbIntelligence.Infrastructure;

public interface IPrerequisiteHealthService
{
    Task<PrerequisiteHealthDto> CheckAsync(CancellationToken cancellationToken = default);
}

public interface IPrerequisiteInstaller
{
    Task<int> InstallAsync(bool assumeYes, TextReader? input, TextWriter? output, CancellationToken cancellationToken = default);
}

public sealed class PrerequisiteHealthService : IPrerequisiteHealthService
{
    private readonly CliProcessRunner _runner;
    private readonly DbIntelligenceOptions _options;

    public PrerequisiteHealthService(CliProcessRunner runner, IOptions<DbIntelligenceOptions> options)
    {
        _runner = runner;
        _options = options.Value;
    }

    public async Task<PrerequisiteHealthDto> CheckAsync(CancellationToken cancellationToken = default)
    {
        var python = await DetectPythonAsync(cancellationToken);
        var pip = await DetectPipAsync(python, cancellationToken);
        var codegraph = await DetectCommandAsync(
            _options.CodegraphExecutable,
            ["-V"],
            "codegraph",
            "Install with: .\\scripts\\Initialize-DbIntelligenceNode.ps1 -InstallCodegraph -Yes   (or: fnm exec --using=lts-latest -- npm i -g @colbymchenry/codegraph)",
            cancellationToken);

        var graphify = await DetectGraphifyAsync(cancellationToken);

        var dto = new PrerequisiteHealthDto
        {
            Python = python,
            Pip = pip,
            Graphify = graphify,
            Codegraph = codegraph
        };

        if (!python.Available) dto.Missing.Add("python");
        if (!pip.Available) dto.Missing.Add("pip");
        if (!graphify.Available) dto.Missing.Add("graphify");
        if (!codegraph.Available) dto.Missing.Add("codegraph");

        dto.Healthy = dto.Missing.Count == 0;
        dto.Status = dto.Healthy ? "healthy" : "unhealthy";
        dto.Message = dto.Healthy
            ? "All prerequisites are available on PATH (python, pip, graphify, codegraph)."
            : $"Missing prerequisites: {string.Join(", ", dto.Missing)}. " +
              "Run DbIntelligence.Cli with --install-preqs to install interactively.";

        return dto;
    }

    private async Task<PrerequisiteCheckDto> DetectPythonAsync(CancellationToken cancellationToken)
    {
        foreach (var (exe, args) in PythonCandidates())
        {
            var result = await _runner.RunAsync(exe, args, timeoutSeconds: 20, cancellationToken: cancellationToken);
            var text = $"{result.StandardOutput}\n{result.StandardError}";
            if (result.Succeeded || Regex.IsMatch(text, @"Python\s+\d+\.\d+", RegexOptions.IgnoreCase))
            {
                var match = Regex.Match(text, @"Python\s+[\d.]+", RegexOptions.IgnoreCase);
                return new PrerequisiteCheckDto
                {
                    Name = "python",
                    Available = true,
                    Executable = exe,
                    VersionOrDetail = match.Success ? match.Value : text.Trim().Split('\n')[0].Trim()
                };
            }
        }

        return new PrerequisiteCheckDto
        {
            Name = "python",
            Available = false,
            Remediation = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "Install via winget: winget install Python.Python.3.12"
                : "Install Python 3.10+ from https://www.python.org/downloads/"
        };
    }

    private async Task<PrerequisiteCheckDto> DetectPipAsync(PrerequisiteCheckDto python, CancellationToken cancellationToken)
    {
        if (!python.Available || string.IsNullOrWhiteSpace(python.Executable))
        {
            return new PrerequisiteCheckDto
            {
                Name = "pip",
                Available = false,
                Remediation = "Install Python first, then ensure `python -m pip` works."
            };
        }

        var exe = python.Executable;
        var args = exe.Equals("py", StringComparison.OrdinalIgnoreCase)
            ? new[] { "-3", "-m", "pip", "--version" }
            : new[] { "-m", "pip", "--version" };

        var result = await _runner.RunAsync(exe, args, timeoutSeconds: 30, cancellationToken: cancellationToken);
        var text = $"{result.StandardOutput}\n{result.StandardError}";
        if (result.Succeeded || text.Contains("pip", StringComparison.OrdinalIgnoreCase))
        {
            return new PrerequisiteCheckDto
            {
                Name = "pip",
                Available = true,
                Executable = exe,
                VersionOrDetail = text.Trim().Split('\n')[0].Trim()
            };
        }

        return new PrerequisiteCheckDto
        {
            Name = "pip",
            Available = false,
            Executable = exe,
            Remediation = $"Run: {exe} -m ensurepip --upgrade"
        };
    }

    private async Task<PrerequisiteCheckDto> DetectGraphifyAsync(CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(
            _options.GraphifyExecutable,
            ["--help"],
            timeoutSeconds: 30,
            cancellationToken: cancellationToken);
        var text = $"{result.StandardOutput}\n{result.StandardError}";
        var available = text.Contains("extract", StringComparison.OrdinalIgnoreCase)
            || text.Contains("update <path>", StringComparison.OrdinalIgnoreCase)
            || text.Contains("graphify-out", StringComparison.OrdinalIgnoreCase);

        return new PrerequisiteCheckDto
        {
            Name = "graphify",
            Available = available,
            Executable = _options.GraphifyExecutable,
            VersionOrDetail = available ? FirstLine(text) : null,
            Remediation = available
                ? null
                : "Install Graphify-Labs CLI: python -m pip install graphifyy  (must expose `graphify extract`)"
        };
    }

    private async Task<PrerequisiteCheckDto> DetectCommandAsync(
        string executable,
        IReadOnlyList<string> args,
        string name,
        string remediation,
        CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(executable, args, timeoutSeconds: 30, cancellationToken: cancellationToken);
        var text = $"{result.StandardOutput}\n{result.StandardError}";
        var startFailed = text.Contains("Failed to start", StringComparison.OrdinalIgnoreCase)
            || text.Contains("cannot find the file", StringComparison.OrdinalIgnoreCase)
            || result.ExitCode == -1 && string.IsNullOrWhiteSpace(result.StandardOutput);

        var available = !startFailed && (
            result.Succeeded
            || (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StandardOutput))
            || (result.StandardOutput.Contains(name, StringComparison.OrdinalIgnoreCase)
                && !result.StandardError.Contains("Failed to start", StringComparison.OrdinalIgnoreCase)));

        return new PrerequisiteCheckDto
        {
            Name = name,
            Available = available,
            Executable = executable,
            VersionOrDetail = available ? FirstLine(text) : null,
            Remediation = available ? null : remediation
        };
    }

    private static IEnumerable<(string Exe, string[] Args)> PythonCandidates()
    {
        yield return ("python", ["--version"]);
        yield return ("py", ["-3", "--version"]);
        yield return ("python3", ["--version"]);
    }

    private static string FirstLine(string text)
    {
        var line = text.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? text.Trim();
        return line.Length <= 200 ? line : line[..200];
    }
}

public sealed class PrerequisiteInstaller : IPrerequisiteInstaller
{
    private readonly CliProcessRunner _runner;
    private readonly IPrerequisiteHealthService _health;

    public PrerequisiteInstaller(CliProcessRunner runner, IPrerequisiteHealthService health)
    {
        _runner = runner;
        _health = health;
    }

    public async Task<int> InstallAsync(
        bool assumeYes,
        TextReader? input,
        TextWriter? output,
        CancellationToken cancellationToken = default)
    {
        input ??= Console.In;
        output ??= Console.Out;

        await output.WriteLineAsync("DbIntelligence prerequisite installer");
        await output.WriteLineAsync("Checks: python, pip, graphify (PyPI graphifyy), codegraph (fnm exec npm when fnm is present)");
        await output.WriteLineAsync();

        var before = await _health.CheckAsync(cancellationToken);
        PrintHealth(output, before);

        if (before.Healthy)
        {
            await output.WriteLineAsync("Nothing to install — all prerequisites are already available.");
            return 0;
        }

        if (!before.Python.Available)
        {
            if (!await ConfirmAsync(input, output, assumeYes,
                    "Python is not installed. Install Python 3.12 via winget now?"))
            {
                await output.WriteLineAsync("Skipped Python install. Graphify cannot be installed without Python.");
                return 1;
            }

            var ok = await InstallPythonAsync(output, cancellationToken);
            if (!ok)
            {
                await output.WriteLineAsync("Python install failed or winget is unavailable. Install Python manually, then re-run --install-preqs.");
                return 1;
            }
        }

        var healthAfterPython = await _health.CheckAsync(cancellationToken);
        if (!healthAfterPython.Pip.Available && healthAfterPython.Python.Available)
        {
            if (await ConfirmAsync(input, output, assumeYes, "pip is missing. Run `python -m ensurepip --upgrade`?"))
            {
                await RunPythonModuleAsync(healthAfterPython.Python.Executable!, ["ensurepip", "--upgrade"], output, cancellationToken);
            }
        }

        healthAfterPython = await _health.CheckAsync(cancellationToken);
        if (!healthAfterPython.Graphify.Available)
        {
            if (!healthAfterPython.Python.Available || !healthAfterPython.Pip.Available)
            {
                await output.WriteLineAsync("Cannot install graphify without python + pip.");
                return 1;
            }

            if (await ConfirmAsync(input, output, assumeYes,
                    "Install Graphify now? (`python -m pip install graphifyy`)"))
            {
                var pipArgs = healthAfterPython.Python.Executable!.Equals("py", StringComparison.OrdinalIgnoreCase)
                    ? new[] { "-3", "-m", "pip", "install", "--upgrade", "graphifyy" }
                    : new[] { "-m", "pip", "install", "--upgrade", "graphifyy" };
                var pip = await _runner.RunAsync(
                    healthAfterPython.Python.Executable!,
                    pipArgs,
                    timeoutSeconds: 600,
                    cancellationToken: cancellationToken);
                await output.WriteLineAsync(pip.Succeeded
                    ? "Graphify package installed (graphifyy). Ensure `graphify extract --help` works in a new terminal."
                    : $"Graphify install failed: {pip.StandardError}");
                if (!pip.Succeeded)
                    return 1;
            }
        }

        var healthMid = await _health.CheckAsync(cancellationToken);
        if (!healthMid.Codegraph.Available)
        {
            var preferFnm = await IsFnmAvailableAsync(cancellationToken);
            var prompt = preferFnm
                ? "Install Codegraph now (`fnm exec --using=lts-latest -- npm i -g @colbymchenry/codegraph`)?"
                : "Install Codegraph now (`npm i -g @colbymchenry/codegraph`)?";

            if (await ConfirmAsync(input, output, assumeYes, prompt))
            {
                var npmOk = await InstallCodegraphViaNpmAsync(output, preferFnm, cancellationToken);
                if (!npmOk)
                {
                    await output.WriteLineAsync("Falling back to official install.ps1...");
                    var ok = await InstallCodegraphAsync(output, cancellationToken);
                    if (!ok)
                        await output.WriteLineAsync("Codegraph install did not complete successfully.");
                }
            }
        }

        var after = await _health.CheckAsync(cancellationToken);
        await output.WriteLineAsync();
        await output.WriteLineAsync("Post-install health check:");
        PrintHealth(output, after);
        return after.Healthy ? 0 : 2;
    }

    private async Task<bool> InstallPythonAsync(TextWriter output, CancellationToken cancellationToken)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            await output.WriteLineAsync("Automatic Python install is only scripted for Windows (winget). Install Python 3.10+ manually.");
            return false;
        }

        await output.WriteLineAsync("Running: winget install -e --id Python.Python.3.12 --accept-package-agreements --accept-source-agreements");
        var result = await _runner.RunAsync(
            "winget",
            ["install", "-e", "--id", "Python.Python.3.12", "--accept-package-agreements", "--accept-source-agreements"],
            timeoutSeconds: 900,
            cancellationToken: cancellationToken);

        await output.WriteLineAsync(result.Succeeded
            ? "winget Python install finished. Open a new terminal if `python` is not yet on PATH."
            : $"winget failed (exit {result.ExitCode}): {FirstLine(result.StandardError + result.StandardOutput)}");
        return result.Succeeded;
    }

    private async Task RunPythonModuleAsync(
        string pythonExe,
        string[] moduleArgs,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var args = pythonExe.Equals("py", StringComparison.OrdinalIgnoreCase)
            ? new[] { "-3", "-m" }.Concat(moduleArgs).ToArray()
            : new[] { "-m" }.Concat(moduleArgs).ToArray();
        var result = await _runner.RunAsync(pythonExe, args, timeoutSeconds: 180, cancellationToken: cancellationToken);
        await output.WriteLineAsync(result.Succeeded
            ? $"OK: {pythonExe} -m {string.Join(' ', moduleArgs)}"
            : $"Failed: {FirstLine(result.StandardError)}");
    }

    private async Task<bool> IsFnmAvailableAsync(CancellationToken cancellationToken)
    {
        var probe = await _runner.RunAsync(
            "fnm",
            ["--version"],
            timeoutSeconds: 30,
            cancellationToken: cancellationToken);
        if (!probe.Succeeded)
            return false;

        var text = $"{probe.StandardOutput}\n{probe.StandardError}";
        return !text.Contains("Failed to start", StringComparison.OrdinalIgnoreCase)
               && !text.Contains("cannot find the file", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> InstallCodegraphViaNpmAsync(
        TextWriter output,
        bool preferFnm,
        CancellationToken cancellationToken)
    {
        const string package = "@colbymchenry/codegraph";

        if (preferFnm)
        {
            // Bare `fnm exec --` fails without .node-version/.nvmrc even when a default is set.
            const string fnmNode = "lts-latest";
            await output.WriteLineAsync($"Ensuring Node {fnmNode} via fnm...");
            var ensureNode = await _runner.RunAsync(
                "fnm",
                ["install", fnmNode],
                timeoutSeconds: 600,
                cancellationToken: cancellationToken);
            if (!ensureNode.Succeeded)
            {
                await output.WriteLineAsync(
                    $"fnm install {fnmNode} failed: {FirstLine(ensureNode.StandardError + ensureNode.StandardOutput)}");
            }
            else
            {
                _ = await _runner.RunAsync(
                    "fnm",
                    ["default", fnmNode],
                    timeoutSeconds: 60,
                    cancellationToken: cancellationToken);
            }

            var fnmCmd = $"fnm exec --using={fnmNode} -- npm i -g {package}";
            await output.WriteLineAsync($"fnm detected — installing Codegraph with: {fnmCmd}");
            var viaFnm = await _runner.RunAsync(
                "fnm",
                ["exec", $"--using={fnmNode}", "--", "npm", "i", "-g", package],
                timeoutSeconds: 600,
                cancellationToken: cancellationToken);
            if (viaFnm.Succeeded)
            {
                await output.WriteLineAsync("Codegraph installed via fnm exec + npm. Verify with: codegraph -V");
                return true;
            }

            await output.WriteLineAsync(
                $"fnm exec npm install failed: {FirstLine(viaFnm.StandardError + viaFnm.StandardOutput)}");
            await output.WriteLineAsync("Retrying with PATH npm...");
        }

        await output.WriteLineAsync($"Installing Codegraph with: npm i -g {package}");
        var npm = await _runner.RunAsync(
            "npm",
            ["i", "-g", package],
            timeoutSeconds: 600,
            cancellationToken: cancellationToken);
        await output.WriteLineAsync(npm.Succeeded
            ? "Codegraph installed via npm. Verify with: codegraph -V"
            : $"Codegraph npm install failed: {FirstLine(npm.StandardError + npm.StandardOutput)}");
        return npm.Succeeded;
    }

    private async Task<bool> InstallCodegraphAsync(TextWriter output, CancellationToken cancellationToken)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            await output.WriteLineAsync("Running Codegraph Windows installer via PowerShell...");
            var ps = await _runner.RunAsync(
                "powershell",
                [
                    "-NoProfile",
                    "-ExecutionPolicy", "Bypass",
                    "-Command",
                    "irm https://raw.githubusercontent.com/colbymchenry/codegraph/main/install.ps1 | iex"
                ],
                timeoutSeconds: 600,
                cancellationToken: cancellationToken);
            await output.WriteLineAsync(ps.Succeeded
                ? "Codegraph installer completed."
                : $"Codegraph installer failed: {FirstLine(ps.StandardError + ps.StandardOutput)}");
            return ps.Succeeded;
        }

        await output.WriteLineAsync("Running Codegraph install.sh...");
        var sh = await _runner.RunAsync(
            "bash",
            ["-lc", "curl -fsSL https://raw.githubusercontent.com/colbymchenry/codegraph/main/install.sh | sh"],
            timeoutSeconds: 600,
            cancellationToken: cancellationToken);
        return sh.Succeeded;
    }

    private static async Task<bool> ConfirmAsync(TextReader input, TextWriter output, bool assumeYes, string question)
    {
        if (assumeYes)
        {
            await output.WriteLineAsync($"{question} [Y] (--yes)");
            return true;
        }

        await output.WriteAsync($"{question} [y/N]: ");
        var line = await input.ReadLineAsync();
        return !string.IsNullOrWhiteSpace(line) &&
               (line.Equals("y", StringComparison.OrdinalIgnoreCase) ||
                line.Equals("yes", StringComparison.OrdinalIgnoreCase));
    }

    private static void PrintHealth(TextWriter output, PrerequisiteHealthDto health)
    {
        output.WriteLine($"Status: {health.Status}");
        WriteCheck(output, health.Python);
        WriteCheck(output, health.Pip);
        WriteCheck(output, health.Graphify);
        WriteCheck(output, health.Codegraph);
        if (health.Missing.Count > 0)
            output.WriteLine($"Missing: {string.Join(", ", health.Missing)}");
        output.WriteLine();
    }

    private static void WriteCheck(TextWriter output, PrerequisiteCheckDto check)
    {
        var mark = check.Available ? "OK" : "MISSING";
        var detail = check.Available ? check.VersionOrDetail : check.Remediation;
        output.WriteLine($"  [{mark}] {check.Name}: {detail}");
    }

    private static string FirstLine(string text) =>
        text.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? text.Trim();
}
