using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FindingsMigration.Contracts;

namespace FindingsMigration.Core;

public sealed class SpHierarchyAnalyzer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public SpHierarchyResult Analyze(
        StoredProcedureMapDocument spMap,
        string rootProcedureName,
        string? inventoryPathOrJson = null)
    {
        if (spMap.Procedures.Count > 0 && !IsProcedureInMap(spMap, rootProcedureName))
            throw new InvalidOperationException($"Root procedure is not present in --sp-map: {rootProcedureName}");

        var normalizedRoot = NormalizeFqn(rootProcedureName);

        SpDependencyInventoryDocument? inventory = null;
        if (!string.IsNullOrWhiteSpace(inventoryPathOrJson))
        {
            var raw = File.Exists(inventoryPathOrJson)
                ? File.ReadAllText(inventoryPathOrJson)
                : inventoryPathOrJson;
            inventory = JsonSerializer.Deserialize<SpDependencyInventoryDocument>(raw, JsonOptions)
                        ?? new SpDependencyInventoryDocument();
        }

        if (inventory is not null && inventory.ProcedureEdges.Count > 0)
            return AnalyzeFromInventory(normalizedRoot, inventory);

        // Fallback: no inventory => best-effort: show tables from spMap reads/writes only.
        return AnalyzeFromSpMapOnly(spMap, normalizedRoot);
    }

    public string RenderTree(SpHierarchyResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Root: {result.RootProcedure}");
        foreach (var dep in result.Dependencies)
        {
            RenderNode(sb, dep, indentLevel: 0);
        }
        return sb.ToString();
    }

    private static void RenderNode(
        StringBuilder sb,
        SpDependencyNode node,
        int indentLevel)
    {
        var indent = new string(' ', indentLevel * 2);

        if (node.Kind.Equals("TABLE", StringComparison.OrdinalIgnoreCase) && node.ColumnUsage is not null)
        {
            sb.AppendLine(
                $"{indent}- {node.Kind}: {node.Schema}.{node.Name} ({node.ColumnUsage.UsedColumns}/{node.ColumnUsage.TotalColumns} used)");
            sb.AppendLine($"{indent}  used: {string.Join(", ", node.ColumnUsage.Used)}");
            sb.AppendLine($"{indent}  unused: {string.Join(", ", node.ColumnUsage.Unused)}");
        }
        else
        {
            sb.AppendLine($"{indent}- {node.Kind}: {node.Schema}.{node.Name}");
        }

        foreach (var child in node.Children)
        {
            RenderNode(sb, child, indentLevel + 1);
        }
    }

    private SpHierarchyResult AnalyzeFromInventory(
        string rootProcedureFqn,
        SpDependencyInventoryDocument inventory)
    {
        var procEdgesByParent = inventory.ProcedureEdges
            .GroupBy(e => NormalizeFqn(e.ParentProcedureFqn), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => NormalizeFqn(x.ChildProcedureFqn)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                StringComparer.OrdinalIgnoreCase);

        var tableRowsByProcedure = inventory.TableColumnUsage
            .GroupBy(r => NormalizeFqn(r.ProcedureFqn), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var typesByProcedure = inventory.TypeDependencies
            .GroupBy(d => NormalizeFqn(d.ProcedureFqn), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => NormalizeFqn(x.TypeFqn)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                StringComparer.OrdinalIgnoreCase);

        var viewsByProcedure = inventory.ViewDependencies
            .GroupBy(d => NormalizeFqn(d.ProcedureFqn), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => NormalizeFqn(x.ViewFqn)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                StringComparer.OrdinalIgnoreCase);

        var rootNode = BuildProcedureNode(
            rootProcedureFqn,
            depth: 0,
            parentProcedureFqn: null,
            inPath: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            procEdgesByParent,
            tableRowsByProcedure,
            typesByProcedure,
            viewsByProcedure);

        // Root procedure is already provided separately in SpHierarchyResult.RootProcedure.
        return new SpHierarchyResult
        {
            RootProcedure = rootProcedureFqn,
            Dependencies = rootNode.Children
        };
    }

    private SpDependencyNode BuildProcedureNode(
        string procedureFqn,
        int depth,
        string? parentProcedureFqn,
        HashSet<string> inPath,
        Dictionary<string, List<string>> procEdgesByParent,
        Dictionary<string, List<SpTableColumnUsageRow>> tableRowsByProcedure,
        Dictionary<string, List<string>> typesByProcedure,
        Dictionary<string, List<string>> viewsByProcedure)
    {
        var normalized = NormalizeFqn(procedureFqn);
        if (!inPath.Add(normalized))
        {
            // Cycle detected; emit a leaf node to avoid infinite recursion.
            var (schema, name) = ParseSchemaAndName(normalized);
            return new SpDependencyNode
            {
                Kind = "PROCEDURE",
                Schema = schema,
                Name = name,
                Depth = depth,
                ParentProcedure = parentProcedureFqn
            };
        }

        var node = new SpDependencyNode
        {
            Kind = "PROCEDURE",
            Depth = depth,
            ParentProcedure = parentProcedureFqn,
        };

        (node.Schema, node.Name) = ParseSchemaAndName(normalized);

        var children = new List<SpDependencyNode>();

        if (procEdgesByParent.TryGetValue(normalized, out var childProcedures))
        {
            foreach (var childProc in childProcedures.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                children.Add(BuildProcedureNode(
                    childProc,
                    depth + 1,
                    normalized,
                    inPath,
                    procEdgesByParent,
                    tableRowsByProcedure,
                    typesByProcedure,
                    viewsByProcedure));
            }
        }

        if (tableRowsByProcedure.TryGetValue(normalized, out var tableRows))
        {
            // For each table, compute column lists and used/unused status.
            foreach (var tableGroup in tableRows
                         .GroupBy(r => NormalizeFqn(r.TableFqn), StringComparer.OrdinalIgnoreCase)
                         .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                var tableFqn = tableGroup.Key;
                var (schema, name) = ParseSchemaAndName(tableFqn);
                var columns = tableGroup
                    .Select(r => new { r.ColumnName, r.IsUsed })
                    .Distinct()
                    .OrderBy(x => x.ColumnName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var used = columns.Where(c => c.IsUsed).Select(c => c.ColumnName).ToList();
                var unused = columns.Where(c => !c.IsUsed).Select(c => c.ColumnName).ToList();

                children.Add(new SpDependencyNode
                {
                    Kind = "TABLE",
                    Schema = schema,
                    Name = name,
                    Depth = depth + 1,
                    ParentProcedure = normalized,
                    ColumnUsage = new TableColumnUsage
                    {
                        TotalColumns = columns.Count,
                        UsedColumns = used.Count,
                        Used = used,
                        Unused = unused
                    }
                });
            }
        }

        if (typesByProcedure.TryGetValue(normalized, out var typeFqns))
        {
            foreach (var typeFqn in typeFqns.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                var (schema, name) = ParseSchemaAndName(typeFqn);
                children.Add(new SpDependencyNode
                {
                    Kind = "TYPE",
                    Schema = schema,
                    Name = name,
                    Depth = depth + 1,
                    ParentProcedure = normalized,
                });
            }
        }

        if (viewsByProcedure.TryGetValue(normalized, out var viewFqns))
        {
            foreach (var viewFqn in viewFqns.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                var (schema, name) = ParseSchemaAndName(viewFqn);
                children.Add(new SpDependencyNode
                {
                    Kind = "VIEW",
                    Schema = schema,
                    Name = name,
                    Depth = depth + 1,
                    ParentProcedure = normalized,
                });
            }
        }

        node.Children = children;
        _ = inPath.Remove(normalized); // allow same procedure in different branches, but block cycles within a path.
        return node;
    }

    private SpHierarchyResult AnalyzeFromSpMapOnly(StoredProcedureMapDocument spMap, string rootProcedureFqn)
    {
        // This fallback has no reliable sub-SP graph and no column-level usage.
        // Still provides the "tables used" part using stored procedure reads/writes.
        var entry = spMap.Procedures.FirstOrDefault(p =>
        {
            foreach (var candidate in EnumerateProcedureFqns(p))
            {
                if (string.Equals(NormalizeFqn(candidate), rootProcedureFqn, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        });

        var (schema, name) = ParseSchemaAndName(rootProcedureFqn);
        var rootNode = new SpDependencyNode
        {
            Kind = "PROCEDURE",
            Schema = schema,
            Name = name,
            Depth = 0
        };

        if (entry is null)
            return new SpHierarchyResult { RootProcedure = rootProcedureFqn, Dependencies = [] };

        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in entry.Reads ?? [])
            tables.Add(NormalizeFqn(r));
        foreach (var w in entry.Writes ?? [])
            tables.Add(NormalizeFqn(w));

        foreach (var t in tables.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            var (tSchema, tName) = ParseSchemaAndName(t);
            rootNode.Children.Add(new SpDependencyNode
            {
                Kind = "TABLE",
                Schema = tSchema,
                Name = tName,
                Depth = 1,
                ParentProcedure = rootProcedureFqn,
                ColumnUsage = null
            });
        }

        return new SpHierarchyResult
        {
            RootProcedure = rootProcedureFqn,
            Dependencies = rootNode.Children
        };
    }

    private static bool IsProcedureInMap(StoredProcedureMapDocument spMap, string rootProcedureFqn)
    {
        foreach (var p in spMap.Procedures)
        {
            foreach (var candidate in EnumerateProcedureFqns(p))
            {
                if (string.Equals(NormalizeFqn(candidate), NormalizeFqn(rootProcedureFqn), StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return false;
    }

    private static IEnumerable<string> EnumerateProcedureFqns(StoredProcedureEntry p)
    {
        if (p.ResolvedNames is not null && p.ResolvedNames.Count > 0)
        {
            foreach (var n in p.ResolvedNames)
            {
                if (!string.IsNullOrWhiteSpace(n))
                    yield return n;
            }
        }

        if (!string.IsNullOrWhiteSpace(p.Schema) && !string.IsNullOrWhiteSpace(p.Name) && !p.Name.Contains('{'))
        {
            yield return $"{p.Schema}.{p.Name}";
        }

        // Last-resort: user may store full fqn in Name.
        if (!string.IsNullOrWhiteSpace(p.Name) && p.Name.Contains('.'))
            yield return p.Name;
    }

    private static (string schema, string name) ParseSchemaAndName(string fqn)
    {
        var normalized = NormalizeFqn(fqn);
        var idx = normalized.IndexOf('.', StringComparison.Ordinal);
        if (idx < 0)
            return ("", normalized);
        return (normalized.Substring(0, idx), normalized[(idx + 1)..]);
    }

    /// <summary>
    /// Normalize FQN strings from SQL inventory.
    /// Example: "[dbo].[Customers]" -> "dbo.Customers"
    /// </summary>
    private static string NormalizeFqn(string fqn)
    {
        if (string.IsNullOrWhiteSpace(fqn))
            return "";

        var s = fqn.Trim();
        s = s.Replace("].[", ".", StringComparison.Ordinal);
        s = s.Replace("[", "", StringComparison.Ordinal);
        s = s.Replace("]", "", StringComparison.Ordinal);
        return s;
    }

    // Inventory model (optional): JSON snapshot generated from SQL script 50.
    internal sealed class SpDependencyInventoryDocument
    {
        [JsonPropertyName("rootProcedure")]
        public string RootProcedure { get; set; } = "";

        [JsonPropertyName("procedureEdges")]
        public List<ProcedureEdgeRow> ProcedureEdges { get; set; } = [];

        [JsonPropertyName("tableColumnUsage")]
        public List<SpTableColumnUsageRow> TableColumnUsage { get; set; } = [];

        [JsonPropertyName("typeDependencies")]
        public List<TypeDependencyRow> TypeDependencies { get; set; } = [];

        [JsonPropertyName("viewDependencies")]
        public List<ViewDependencyRow> ViewDependencies { get; set; } = [];
    }

    internal sealed class ProcedureEdgeRow
    {
        // From SQL script result set 1: ParentProcedureFqn + ChildProcedureFqn.
        [JsonPropertyName("ParentProcedureFqn")]
        public string ParentProcedureFqn { get; set; } = "";

        [JsonPropertyName("ChildProcedureFqn")]
        public string ChildProcedureFqn { get; set; } = "";

        [JsonPropertyName("ChildDepth")]
        public int ChildDepth { get; set; }
    }

    internal sealed class SpTableColumnUsageRow
    {
        [JsonPropertyName("ProcedureFqn")]
        public string ProcedureFqn { get; set; } = "";

        [JsonPropertyName("TableFqn")]
        public string TableFqn { get; set; } = "";

        [JsonPropertyName("ColumnName")]
        public string ColumnName { get; set; } = "";

        [JsonPropertyName("IsUsed")]
        public bool IsUsed { get; set; }
    }

    internal sealed class TypeDependencyRow
    {
        [JsonPropertyName("ProcedureFqn")]
        public string ProcedureFqn { get; set; } = "";

        [JsonPropertyName("TypeFqn")]
        public string TypeFqn { get; set; } = "";
    }

    internal sealed class ViewDependencyRow
    {
        [JsonPropertyName("ProcedureFqn")]
        public string ProcedureFqn { get; set; } = "";

        [JsonPropertyName("ViewFqn")]
        public string ViewFqn { get; set; } = "";
    }
}

