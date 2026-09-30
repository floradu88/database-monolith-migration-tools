using System.Text.RegularExpressions;

namespace MigrationTool.Domain;

public static partial class MissingColumnParser
{
    [GeneratedRegex(@"Invalid column name\s+'(?<column>[^']+)'", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SqlServerColumn();

    [GeneratedRegex(
        """
        column\s+"(?<column>(?:[^"]|"")*)"(?:\s+of\s+relation\s+"(?<table>(?:[^"]|"")*)")?
        """,
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PostgresColumn();

    public static MissingColumnFault? Parse(string? errorText, int? errorNumber, string? sqlState, string scriptSql)
    {
        var text = errorText ?? "";
        var missing = errorNumber == 207
            || string.Equals(sqlState, "42703", StringComparison.Ordinal)
            || text.Contains("Invalid column name", StringComparison.OrdinalIgnoreCase)
            || (text.Contains("column", StringComparison.OrdinalIgnoreCase)
                && text.Contains("does not exist", StringComparison.OrdinalIgnoreCase));

        if (!missing)
        {
            return null;
        }

        string? column = null;
        string? table = null;
        string? schema = null;

        var sqlServer = SqlServerColumn().Match(text);
        if (sqlServer.Success)
        {
            column = sqlServer.Groups["column"].Value;
        }

        var postgres = PostgresColumn().Match(text);
        if (postgres.Success)
        {
            column ??= Unquote(postgres.Groups["column"].Value);
            if (postgres.Groups["table"].Success && postgres.Groups["table"].Value.Length > 0)
            {
                table = Unquote(postgres.Groups["table"].Value);
            }
        }

        if (string.IsNullOrWhiteSpace(column))
        {
            return null;
        }

        var fromScript = ScriptTableReference.Find(scriptSql, column);
        schema = fromScript.Schema;
        table ??= fromScript.Table;
        return new MissingColumnFault(column, table, schema);
    }

    private static string Unquote(string value) => value.Replace("\"\"", "\"", StringComparison.Ordinal);
}
