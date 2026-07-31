using System.Text.RegularExpressions;
using DbIntelligence.Domain;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DbIntelligence.RepositoryScanner;

public sealed class RepositoryScannerService
{
    private static readonly Regex ObjectNameRegex = new(
        @"\b(?:dbo|sys|\[?\w+\]?)\.\[?(?<name>\w+)\]?|\b(?<name>usp_\w+|sp_\w+|fn_\w+|tvf_\w+)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FromTableRegex = new(
        @"\bFROM\s+(?<obj>(?:\[[^\]]+\]|\w+)(?:\.(?:\[[^\]]+\]|\w+))?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex JoinTableRegex = new(
        @"\bJOIN\s+(?<obj>(?:\[[^\]]+\]|\w+)(?:\.(?:\[[^\]]+\]|\w+))?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex InsertTableRegex = new(
        @"\bINSERT\s+INTO\s+(?<obj>(?:\[[^\]]+\]|\w+)(?:\.(?:\[[^\]]+\]|\w+))?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex UpdateTableRegex = new(
        @"\bUPDATE\s+(?<obj>(?:\[[^\]]+\]|\w+)(?:\.(?:\[[^\]]+\]|\w+))?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DeleteTableRegex = new(
        @"\bDELETE\s+FROM\s+(?<obj>(?:\[[^\]]+\]|\w+)(?:\.(?:\[[^\]]+\]|\w+))?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ExecProcRegex = new(
        @"\bEXEC(?:UTE)?\s+(?<obj>(?:\[[^\]]+\]|\w+)(?:\.(?:\[[^\]]+\]|\w+))?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<IReadOnlyList<CodeReferenceFinding>> ScanAsync(
        string repositoryPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
            throw new DirectoryNotFoundException($"Repository path not found: {repositoryPath}");

        var findings = new List<CodeReferenceFinding>();
        var csFiles = Directory.EnumerateFiles(repositoryPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

        foreach (var file in csFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var text = await File.ReadAllTextAsync(file, cancellationToken);
            var tree = CSharpSyntaxTree.ParseText(text, path: file, cancellationToken: cancellationToken);
            var root = (CompilationUnitSyntax)await tree.GetRootAsync(cancellationToken);
            findings.AddRange(ScanCompilationUnit(repositoryPath, file, root));
        }

        var sqlFiles = Directory.EnumerateFiles(repositoryPath, "*.sql", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

        foreach (var sqlFile in sqlFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var text = await File.ReadAllTextAsync(sqlFile, cancellationToken);
            findings.AddRange(ScanSqlText(repositoryPath, sqlFile, memberName: Path.GetFileName(sqlFile), text, lineBase: 1, pattern: "embedded-sql-file"));
        }

        return findings;
    }

    public EvidenceGraph ToGraph(IEnumerable<CodeReferenceFinding> findings, string? database = null)
    {
        var graph = new EvidenceGraph();
        graph.Meta.Sources.Add("repository-scanner");

        foreach (var finding in findings)
        {
            var typeName = finding.TypeName ?? "UnknownType";
            var member = finding.MemberName ?? "UnknownMember";
            var codeId = GraphIds.CodeMethod(typeName, member);

            graph.UpsertNode(new GraphNode
            {
                Id = codeId,
                Label = $"{typeName}.{member}",
                Kind = NodeKind.Method,
                SourceFile = finding.FilePath,
                SourceLocation = $"L{finding.Line}"
            });

            var (schema, name, kind) = ClassifyDbObject(finding.NormalizedObjectName, finding.AccessType);
            var dbId = GraphIds.DbObject(database, schema, name, kind);
            graph.UpsertNode(new GraphNode
            {
                Id = dbId,
                Label = string.IsNullOrWhiteSpace(schema) ? name : $"{schema}.{name}",
                Kind = kind,
                Schema = schema,
                Database = database
            });

            graph.UpsertEdge(new GraphEdge
            {
                Source = codeId,
                Target = dbId,
                Relation = finding.AccessType,
                Confidence = finding.Confidence,
                Evidence = new EdgeEvidence
                {
                    File = finding.FilePath,
                    Line = finding.Line,
                    Pattern = finding.Pattern,
                    RawReference = finding.RawReference
                }
            });
        }

        return graph;
    }

    private static IEnumerable<CodeReferenceFinding> ScanCompilationUnit(
        string repositoryPath,
        string filePath,
        CompilationUnitSyntax root)
    {
        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            var typeName = method.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()?.Identifier.Text ?? "Global";
            var memberName = method.Identifier.Text;
            var methodText = method.ToFullString();
            foreach (var finding in ScanMethodBody(repositoryPath, filePath, typeName, memberName, method, methodText))
                yield return finding;
        }
    }

    private static IEnumerable<CodeReferenceFinding> ScanMethodBody(
        string repositoryPath,
        string filePath,
        string typeName,
        string memberName,
        MethodDeclarationSyntax method,
        string methodText)
    {
        foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var invText = invocation.ToString();
            var line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

            if (LooksLike(invText, "FromSqlRaw", "FromSqlInterpolated", "ExecuteSqlRaw", "ExecuteSqlInterpolated"))
            {
                foreach (var lit in ExtractStringArgs(invocation))
                {
                    foreach (var f in FromSqlLiteral(repositoryPath, filePath, typeName, memberName, lit, line, "ef-core-sql"))
                        yield return f;
                }
            }

            if (LooksLike(invText, "Query<", "QueryAsync", "Execute(", "ExecuteAsync", "QueryMultiple", "QueryFirst"))
            {
                foreach (var lit in ExtractStringArgs(invocation))
                {
                    var isProc = !lit.Contains(' ', StringComparison.Ordinal) && ObjectNameRegex.IsMatch(lit);
                    if (isProc)
                    {
                        yield return CreateFinding(repositoryPath, filePath, typeName, memberName, line, lit, NormalizeName(lit),
                            EdgeRelation.Executes, false, Confidence.Extracted, "dapper-procedure");
                    }
                    else
                    {
                        foreach (var f in FromSqlLiteral(repositoryPath, filePath, typeName, memberName, lit, line, "dapper-sql"))
                            yield return f;
                    }
                }
            }
        }

        // SqlCommand CommandType.StoredProcedure + CommandText patterns
        if (methodText.Contains("CommandType.StoredProcedure", StringComparison.Ordinal) ||
            methodText.Contains("CommandType . StoredProcedure", StringComparison.Ordinal))
        {
            foreach (var lit in method.DescendantNodes().OfType<LiteralExpressionSyntax>()
                         .Where(l => l.IsKind(SyntaxKind.StringLiteralExpression)))
            {
                var value = lit.Token.ValueText;
                if (string.IsNullOrWhiteSpace(value) || value.Contains(' ', StringComparison.Ordinal))
                    continue;
                if (!ObjectNameRegex.IsMatch(value) && !value.Contains('.', StringComparison.Ordinal))
                    continue;

                var line = lit.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                yield return CreateFinding(repositoryPath, filePath, typeName, memberName, line, value, NormalizeName(value),
                    EdgeRelation.Executes, false, Confidence.Extracted, "sqlcommand-stored-procedure");
            }
        }

        // Interpolated / concatenated dynamic SQL
        foreach (var interp in method.DescendantNodes().OfType<InterpolatedStringExpressionSyntax>())
        {
            var text = interp.ToString();
            if (!LooksLikeSql(text))
                continue;
            var line = interp.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            foreach (var f in FromSqlLiteral(repositoryPath, filePath, typeName, memberName, text, line, "interpolated-sql", dynamic: true))
                yield return f;
        }
    }

    private static IEnumerable<CodeReferenceFinding> FromSqlLiteral(
        string repositoryPath,
        string filePath,
        string typeName,
        string memberName,
        string sql,
        int line,
        string pattern,
        bool dynamic = false)
    {
        var confidence = dynamic ? Confidence.Ambiguous : Confidence.Extracted;

        foreach (Match m in ExecProcRegex.Matches(sql))
        {
            var obj = NormalizeName(m.Groups["obj"].Value);
            yield return CreateFinding(repositoryPath, filePath, typeName, memberName, line, m.Value, obj,
                EdgeRelation.Executes, dynamic, confidence, pattern);
        }

        foreach (Match m in FromTableRegex.Matches(sql).Cast<Match>().Concat(JoinTableRegex.Matches(sql).Cast<Match>()))
        {
            var obj = NormalizeName(m.Groups["obj"].Value);
            yield return CreateFinding(repositoryPath, filePath, typeName, memberName, line, m.Value, obj,
                EdgeRelation.Reads, dynamic, confidence, pattern);
        }

        foreach (Match m in InsertTableRegex.Matches(sql).Cast<Match>()
                     .Concat(UpdateTableRegex.Matches(sql).Cast<Match>())
                     .Concat(DeleteTableRegex.Matches(sql).Cast<Match>()))
        {
            var obj = NormalizeName(m.Groups["obj"].Value);
            yield return CreateFinding(repositoryPath, filePath, typeName, memberName, line, m.Value, obj,
                EdgeRelation.Writes, dynamic, confidence, pattern);
        }
    }

    private static IEnumerable<CodeReferenceFinding> ScanSqlText(
        string repositoryPath,
        string filePath,
        string memberName,
        string text,
        int lineBase,
        string pattern)
    {
        return FromSqlLiteral(repositoryPath, filePath, "SqlScript", memberName, text, lineBase, pattern);
    }

    private static CodeReferenceFinding CreateFinding(
        string repositoryPath,
        string filePath,
        string typeName,
        string memberName,
        int line,
        string raw,
        string normalized,
        EdgeRelation relation,
        bool isDynamic,
        Confidence confidence,
        string pattern) => new()
    {
        RepositoryPath = repositoryPath,
        FilePath = filePath,
        TypeName = typeName,
        MemberName = memberName,
        Line = line,
        RawReference = raw,
        NormalizedObjectName = normalized,
        AccessType = relation,
        IsDynamic = isDynamic,
        Confidence = confidence,
        Pattern = pattern
    };

    private static IEnumerable<string> ExtractStringArgs(InvocationExpressionSyntax invocation)
    {
        if (invocation.ArgumentList is null)
            yield break;

        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            switch (arg.Expression)
            {
                case LiteralExpressionSyntax lit when lit.IsKind(SyntaxKind.StringLiteralExpression):
                    yield return lit.Token.ValueText;
                    break;
                case InterpolatedStringExpressionSyntax interp:
                    yield return interp.ToString();
                    break;
            }
        }
    }

    private static bool LooksLike(string text, params string[] needles) =>
        needles.Any(n => text.Contains(n, StringComparison.OrdinalIgnoreCase));

    private static bool LooksLikeSql(string text) =>
        text.Contains("SELECT", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("INSERT", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("UPDATE", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("DELETE", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("EXEC", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeName(string value) =>
        value.Replace("[", "", StringComparison.Ordinal)
             .Replace("]", "", StringComparison.Ordinal)
             .Trim();

    private static (string? Schema, string Name, NodeKind Kind) ClassifyDbObject(string normalized, EdgeRelation relation)
    {
        var parts = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string? schema = parts.Length > 1 ? parts[^2] : "dbo";
        var name = parts.Length > 0 ? parts[^1] : normalized;

        NodeKind kind;
        if (name.StartsWith("usp_", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("sp_", StringComparison.OrdinalIgnoreCase) ||
            relation == EdgeRelation.Executes)
            kind = NodeKind.StoredProcedure;
        else if (name.StartsWith("fn_", StringComparison.OrdinalIgnoreCase) ||
                 name.StartsWith("tvf_", StringComparison.OrdinalIgnoreCase))
            kind = NodeKind.Function;
        else if (name.StartsWith("vw_", StringComparison.OrdinalIgnoreCase) ||
                 name.StartsWith("v_", StringComparison.OrdinalIgnoreCase))
            kind = NodeKind.View;
        else
            kind = NodeKind.Table;

        return (schema, name, kind);
    }
}
