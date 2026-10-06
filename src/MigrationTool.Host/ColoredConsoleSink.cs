using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace MigrationTool.Host;

/// <summary>
/// Console sink that uses <see cref="Console.ForegroundColor"/> so colors show in Windows PowerShell.
/// Error/fatal = red, warning = yellow, success information = green.
/// </summary>
public sealed class ColoredConsoleSink : ILogEventSink
{
    private const string OutputTemplate = "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}";
    private readonly MessageTemplateTextFormatter _formatter = new(OutputTemplate);
    private readonly object _sync = new();

    public void Emit(LogEvent logEvent)
    {
        lock (_sync)
        {
            var previous = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = CliConsoleColor.For(logEvent);
                _formatter.Format(logEvent, Console.Out);
                Console.Out.Flush();
            }
            finally
            {
                Console.ForegroundColor = previous;
            }
        }
    }
}

public static class CliConsoleColor
{
    public static ConsoleColor For(LogEvent logEvent)
    {
        if (logEvent.Level is LogEventLevel.Error or LogEventLevel.Fatal)
        {
            return ConsoleColor.Red;
        }

        if (logEvent.Level == LogEventLevel.Warning)
        {
            return ConsoleColor.Yellow;
        }

        if (TryGetInt(logEvent, "Failed") is > 0)
        {
            return ConsoleColor.Red;
        }

        if (TryGetBool(logEvent, "ConnectionSkipped") == true)
        {
            return ConsoleColor.Yellow;
        }

        var template = logEvent.MessageTemplate.Text;
        if (logEvent.Level == LogEventLevel.Information
            && template.StartsWith("Running ", StringComparison.Ordinal))
        {
            return ConsoleColor.Cyan;
        }

        if (logEvent.Level == LogEventLevel.Information && LooksLikeSuccess(template))
        {
            return ConsoleColor.Green;
        }

        return Console.ForegroundColor;
    }

    public static void Write(string text, ConsoleColor color)
    {
        var previous = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
        }
        finally
        {
            Console.ForegroundColor = previous;
        }
    }

    private static bool LooksLikeSuccess(string template) =>
        template.Contains("succeed", StringComparison.OrdinalIgnoreCase)
        || template.Contains(" succeeded", StringComparison.OrdinalIgnoreCase)
        || template.Contains(" OK", StringComparison.Ordinal)
        || template.Contains("ready at", StringComparison.OrdinalIgnoreCase)
        || template.Contains("Ping OK", StringComparison.OrdinalIgnoreCase)
        || template.Contains("finished.", StringComparison.OrdinalIgnoreCase);

    private static int? TryGetInt(LogEvent logEvent, string name)
    {
        if (!logEvent.Properties.TryGetValue(name, out var value) || value is not ScalarValue scalar)
        {
            return null;
        }

        return scalar.Value switch
        {
            int i => i,
            long l => (int)l,
            byte b => b,
            _ => null
        };
    }

    private static bool? TryGetBool(LogEvent logEvent, string name)
    {
        if (!logEvent.Properties.TryGetValue(name, out var value) || value is not ScalarValue scalar)
        {
            return null;
        }

        return scalar.Value is bool b ? b : null;
    }
}
