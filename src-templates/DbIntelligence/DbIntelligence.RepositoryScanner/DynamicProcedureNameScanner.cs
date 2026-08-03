using BuildingBlocks.DataAccess.Abstractions;
using System.Text.RegularExpressions;
using DbIntelligence.Domain;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DbIntelligence.RepositoryScanner;

/// <summary>
/// Finds interpolated / concatenated procedure names like <c>$"{area}_{action}"</c>
/// and expands holes using enums / const strings declared in the same file.
/// </summary>
internal static class DynamicProcedureNameScanner
{
    public static TokenCatalog BuildTokenCatalog(CompilationUnitSyntax root)
    {
        var enums = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var enumDecl in root.DescendantNodes().OfType<EnumDeclarationSyntax>())
        {
            var members = enumDecl.Members.Select(m => m.Identifier.Text).ToList();
            if (members.Count > 0)
                enums[enumDecl.Identifier.Text] = members;
        }

        var constants = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in root.DescendantNodes().OfType<FieldDeclarationSyntax>())
        {
            if (!field.Modifiers.Any(SyntaxKind.ConstKeyword))
                continue;
            foreach (var variable in field.Declaration.Variables)
            {
                if (variable.Initializer?.Value is LiteralExpressionSyntax lit &&
                    lit.IsKind(SyntaxKind.StringLiteralExpression))
                {
                    constants[variable.Identifier.Text] = lit.Token.ValueText;
                }
            }
        }

        return new TokenCatalog(enums, constants);
    }

    public static IEnumerable<CodeReferenceFinding> ScanMethod(
        string repositoryPath,
        string filePath,
        string typeName,
        string memberName,
        MethodDeclarationSyntax method,
        TokenCatalog tokens)
    {
        var treatsAsProcedure = method.ToFullString().Contains("CommandType.StoredProcedure", StringComparison.Ordinal) ||
                                method.ToFullString().Contains("ExecuteSP", StringComparison.OrdinalIgnoreCase) ||
                                method.ToFullString().Contains("ExecuteSp", StringComparison.OrdinalIgnoreCase);

        foreach (var interp in method.DescendantNodes().OfType<InterpolatedStringExpressionSyntax>())
        {
            var source = interp.ToString();
            var line = interp.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            var template = StoredProcedureName.TryParseInterpolatedTemplate(source);
            if (template is null)
                continue;

            if (!treatsAsProcedure && !StoredProcedureName.LooksLikeProcedureTemplate(template))
                continue;
            if (template.Contains("SELECT", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var finding in ExpandFindings(
                         repositoryPath, filePath, typeName, memberName, line, source, template, tokens))
                yield return finding;
        }

        // "usp_" + area + "_" + action  → template usp_{0}_{1} style holes from identifiers
        foreach (var binary in method.DescendantNodes().OfType<BinaryExpressionSyntax>()
                     .Where(b => b.IsKind(SyntaxKind.AddExpression)))
        {
            if (!TryBuildConcatTemplate(binary, out var template, out var raw))
                continue;
            if (!treatsAsProcedure && !StoredProcedureName.LooksLikeProcedureTemplate(template))
                continue;

            var line = binary.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            foreach (var finding in ExpandFindings(
                         repositoryPath, filePath, typeName, memberName, line, raw, template, tokens))
                yield return finding;
        }
    }

    private static IEnumerable<CodeReferenceFinding> ExpandFindings(
        string repositoryPath,
        string filePath,
        string typeName,
        string memberName,
        int line,
        string raw,
        string template,
        TokenCatalog tokens)
    {
        var expansionMap = tokens.BuildExpansionMap(template);
        var resolved = StoredProcedureName.Expand(template, expansionMap);
        var expanded = resolved.Count > 1 ||
                       (resolved.Count == 1 &&
                        !resolved[0].Contains('{', StringComparison.Ordinal));

        if (!expanded || resolved.Count == 0)
        {
            yield return new CodeReferenceFinding
            {
                RepositoryPath = repositoryPath,
                FilePath = filePath,
                TypeName = typeName,
                MemberName = memberName,
                Line = line,
                RawReference = raw,
                NormalizedObjectName = StoredProcedureName.NormalizeProcedureName(template),
                AccessType = EdgeRelation.Executes,
                IsDynamic = true,
                Confidence = Confidence.Ambiguous,
                Pattern = "interpolated-procedure-template"
            };
            yield break;
        }

        foreach (var name in resolved)
        {
            yield return new CodeReferenceFinding
            {
                RepositoryPath = repositoryPath,
                FilePath = filePath,
                TypeName = typeName,
                MemberName = memberName,
                Line = line,
                RawReference = $"{raw} => {name}",
                NormalizedObjectName = name,
                AccessType = EdgeRelation.Executes,
                IsDynamic = true,
                Confidence = Confidence.Inferred,
                Pattern = "interpolated-procedure-expanded"
            };
        }
    }

    private static bool TryBuildConcatTemplate(BinaryExpressionSyntax binary, out string template, out string raw)
    {
        template = "";
        raw = binary.ToString();
        var parts = FlattenAdd(binary).ToList();
        if (parts.Count < 2)
            return false;

        var sb = new System.Text.StringBuilder();
        var holeIndex = 0;
        foreach (var part in parts)
        {
            switch (part)
            {
                case LiteralExpressionSyntax lit when lit.IsKind(SyntaxKind.StringLiteralExpression):
                    sb.Append(lit.Token.ValueText);
                    break;
                case IdentifierNameSyntax id:
                    sb.Append('{').Append(id.Identifier.Text).Append('}');
                    holeIndex++;
                    break;
                case MemberAccessExpressionSyntax member:
                    sb.Append('{').Append(member.Name.Identifier.Text).Append('}');
                    holeIndex++;
                    break;
                case InvocationExpressionSyntax inv when inv.Expression is MemberAccessExpressionSyntax ma &&
                                                         ma.Name.Identifier.Text.Equals("ToString", StringComparison.Ordinal):
                    sb.Append('{').Append(SimplifyLeft(ma.Expression)).Append('}');
                    holeIndex++;
                    break;
                default:
                    sb.Append("{").Append(holeIndex++).Append('}');
                    break;
            }
        }

        template = sb.ToString();
        return StoredProcedureName.LooksLikeProcedureTemplate(template);
    }

    private static string SimplifyLeft(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax id => id.Identifier.Text,
        MemberAccessExpressionSyntax m => m.Name.Identifier.Text,
        _ => "token"
    };

    private static IEnumerable<ExpressionSyntax> FlattenAdd(ExpressionSyntax expression)
    {
        if (expression is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.AddExpression))
        {
            foreach (var left in FlattenAdd(binary.Left))
                yield return left;
            foreach (var right in FlattenAdd(binary.Right))
                yield return right;
            yield break;
        }

        yield return expression;
    }

    internal sealed class TokenCatalog(
        IReadOnlyDictionary<string, IReadOnlyList<string>> enums,
        IReadOnlyDictionary<string, string> constants)
    {
        public IReadOnlyDictionary<string, IReadOnlyList<string>> BuildExpansionMap(string template)
        {
            var map = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (Match hole in Regex.Matches(template, @"\{(?<name>[A-Za-z_][A-Za-z0-9_]*)\}"))
            {
                var name = hole.Groups["name"].Value;
                if (map.ContainsKey(name))
                    continue;

                if (enums.TryGetValue(name, out var enumValues))
                {
                    map[name] = enumValues;
                    continue;
                }

                var fuzzy = enums.FirstOrDefault(e =>
                    e.Key.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                    e.Key.EndsWith(name, StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith(e.Key, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(fuzzy.Key))
                {
                    map[name] = fuzzy.Value;
                    continue;
                }

                if (constants.TryGetValue(name, out var constValue))
                    map[name] = [constValue];
            }

            return map;
        }
    }
}
