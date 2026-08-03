using DbIntelligence.Contracts;
using DbIntelligence.Domain;

namespace DbIntelligence.Infrastructure;

public sealed class EvidenceGraphMerger
{
    public EvidenceGraph Merge(params EvidenceGraph?[] graphs)
    {
        var merged = new EvidenceGraph();
        merged.Meta.GeneratedAt = DateTimeOffset.UtcNow;

        foreach (var graph in graphs.Where(g => g is not null))
        {
            foreach (var source in graph!.Meta.Sources)
            {
                if (!merged.Meta.Sources.Contains(source, StringComparer.OrdinalIgnoreCase))
                    merged.Meta.Sources.Add(source);
            }

            merged.Meta.TargetRepositoryPath ??= graph.Meta.TargetRepositoryPath;

            foreach (var node in graph.Nodes)
                merged.UpsertNode(node);

            foreach (var edge in graph.Edges)
                merged.UpsertEdge(edge);
        }

        return merged;
    }

    public GraphifyGraphDto ToGraphifyDto(EvidenceGraph graph) => new()
    {
        Meta = new GraphifyMetaDto
        {
            GeneratedAt = graph.Meta.GeneratedAt,
            Sources = [.. graph.Meta.Sources],
            TargetRepositoryPath = graph.Meta.TargetRepositoryPath
        },
        Nodes = graph.Nodes.Select(n => new GraphifyNodeDto
        {
            Id = n.Id,
            Label = n.Label,
            Kind = n.Kind.ToString(),
            SourceFile = n.SourceFile,
            SourceLocation = n.SourceLocation,
            Community = n.Community,
            Schema = n.Schema,
            Database = n.Database
        }).ToList(),
        Edges = graph.Edges.Select(e => new GraphifyEdgeDto
        {
            Source = e.Source,
            Target = e.Target,
            Relation = ToRelationString(e.Relation),
            Confidence = e.Confidence.ToString().ToUpperInvariant(),
            Evidence = e.Evidence is null ? null : new GraphifyEvidenceDto
            {
                File = e.Evidence.File,
                Line = e.Evidence.Line,
                Pattern = e.Evidence.Pattern,
                RawReference = e.Evidence.RawReference
            }
        }).ToList()
    };

    public CodeToDbMapDto ToCodeToDbMap(EvidenceGraph graph)
    {
        var repo = graph.Meta.TargetRepositoryPath;
        var map = new CodeToDbMapDto
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            RepositoryPath = repo
        };

        foreach (var edge in graph.Edges.Where(IsCodeToDbEdge))
        {
            var code = graph.FindNode(edge.Source);
            var db = graph.FindNode(edge.Target);
            if (code is null || db is null)
                continue;

            var project = code.Properties.GetValueOrDefault(ProjectGraphIds.ProjectPropertyKey)
                          ?? ProjectGraphIds.TryGetProject(code.Id);
            var relation = ToRelationString(edge.Relation);
            var confidence = edge.Confidence.ToString().ToUpperInvariant();
            var locations = GetEdgeLocations(edge, code.SourceFile).ToList();
            var refs = locations.Select(loc => ReferencePathResolver.ToLocationDto(
                loc.File,
                loc.Line,
                repo,
                code.Id,
                code.Label,
                db.Label,
                db.Kind.ToString(),
                relation,
                confidence,
                loc.Pattern,
                loc.RawReference,
                project)).ToList();

            var primary = refs.FirstOrDefault();
            map.Entries.Add(new CodeToDbEntryDto
            {
                CodeNodeId = code.Id,
                CodeLabel = code.Label,
                SourceFile = primary?.RelativePath ?? primary?.FullPath ?? edge.Evidence?.File ?? code.SourceFile,
                SourceFileFullPath = primary?.FullPath,
                Line = primary?.Line,
                Location = primary?.Location,
                DbNodeId = db.Id,
                DbObject = db.Label,
                DbKind = db.Kind.ToString(),
                Relation = relation,
                Confidence = confidence,
                Pattern = primary?.Pattern ?? edge.Evidence?.Pattern,
                Project = project,
                References = refs
            });

            map.References.AddRange(refs);
        }

        map.References = DeduplicateReferences(map.References);
        return map;
    }

    public CodeReferenceLocationsDocument ToCodeReferenceLocations(EvidenceGraph graph)
    {
        var map = ToCodeToDbMap(graph);
        return new CodeReferenceLocationsDocument
        {
            GeneratedAt = map.GeneratedAt,
            RepositoryPath = map.RepositoryPath,
            Count = map.References.Count,
            References = map.References
                .OrderBy(r => r.FullPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Line)
                .ToList()
        };
    }

    public StoredProcedureMapDto ToStoredProcedureMap(EvidenceGraph graph)
    {
        var repo = graph.Meta.TargetRepositoryPath;
        var dto = new StoredProcedureMapDto
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            RepositoryPath = repo
        };
        var procedures = graph.Nodes.Where(n => n.Kind == NodeKind.StoredProcedure).ToList();

        foreach (var proc in procedures)
        {
            var entry = new StoredProcedureMapEntryDto
            {
                Id = proc.Id,
                Name = proc.Label,
                Schema = proc.Schema,
                Database = proc.Database
            };

            foreach (var edge in graph.Edges.Where(e =>
                         string.Equals(e.Target, proc.Id, StringComparison.OrdinalIgnoreCase) &&
                         (e.Relation is EdgeRelation.Executes or EdgeRelation.Calls or EdgeRelation.DependsOn)))
            {
                var caller = graph.FindNode(edge.Source);
                if (caller is null)
                    continue;
                if (ProjectGraphIds.IsCodeNodeId(caller.Id))
                {
                    entry.CodeCallers.Add(caller.Label);
                    foreach (var loc in GetEdgeLocations(edge, caller.SourceFile))
                    {
                        var reference = ReferencePathResolver.ToLocationDto(
                            loc.File,
                            loc.Line,
                            repo,
                            caller.Id,
                            caller.Label,
                            proc.Label,
                            proc.Kind.ToString(),
                            ToRelationString(edge.Relation),
                            edge.Confidence.ToString().ToUpperInvariant(),
                            loc.Pattern,
                            loc.RawReference);
                        entry.References.Add(reference);
                        dto.References.Add(reference);
                    }
                }
                else
                {
                    entry.SqlCallers.Add(caller.Label);
                }
            }

            foreach (var edge in graph.Edges.Where(e => string.Equals(e.Source, proc.Id, StringComparison.OrdinalIgnoreCase)))
            {
                var target = graph.FindNode(edge.Target);
                if (target is null)
                    continue;
                if (edge.Relation == EdgeRelation.Reads)
                    entry.Reads.Add(target.Label);
                else if (edge.Relation is EdgeRelation.Writes or EdgeRelation.DependsOn)
                    entry.Writes.Add(target.Label);
            }

            entry.CodeCallers = entry.CodeCallers.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            entry.SqlCallers = entry.SqlCallers.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            entry.References = DeduplicateReferences(entry.References);
            dto.Procedures.Add(entry);
        }

        dto.References = DeduplicateReferences(dto.References)
            .OrderBy(r => r.FullPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Line)
            .ToList();
        return dto;
    }

    public EvidenceGraph ExploreNeighborhood(EvidenceGraph graph, string query, int depth = 1)
    {
        var result = new EvidenceGraph { Meta = graph.Meta };
        var seeds = graph.Nodes
            .Where(n => n.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || n.Label.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select(n => n.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (seeds.Count == 0)
            return result;

        var include = new HashSet<string>(seeds, StringComparer.OrdinalIgnoreCase);
        for (var d = 0; d < depth; d++)
        {
            var frontier = graph.Edges
                .Where(e => include.Contains(e.Source) || include.Contains(e.Target))
                .SelectMany(e => new[] { e.Source, e.Target })
                .ToList();
            foreach (var id in frontier)
                include.Add(id);
        }

        foreach (var node in graph.Nodes.Where(n => include.Contains(n.Id)))
            result.UpsertNode(node);
        foreach (var edge in graph.Edges.Where(e => include.Contains(e.Source) && include.Contains(e.Target)))
            result.UpsertEdge(edge);

        return result;
    }

    private static IEnumerable<EdgeEvidence> GetEdgeLocations(GraphEdge edge, string? fallbackFile)
    {
        if (edge.Locations.Count > 0)
            return edge.Locations;

        if (edge.Evidence is not null)
            return [edge.Evidence];

        if (!string.IsNullOrWhiteSpace(fallbackFile))
            return [new EdgeEvidence { File = fallbackFile }];

        return [];
    }

    private static List<CodeReferenceLocationDto> DeduplicateReferences(IEnumerable<CodeReferenceLocationDto> refs) =>
        refs
            .GroupBy(r => $"{r.FullPath}|{r.Line}|{r.DbObject}|{r.Relation}|{r.CodeLabel}", StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

    private static bool IsCodeToDbEdge(GraphEdge edge) =>
        ProjectGraphIds.IsCodeNodeId(edge.Source) &&
        ProjectGraphIds.IsDbNodeId(edge.Target) &&
        edge.Relation is EdgeRelation.Executes or EdgeRelation.Reads or EdgeRelation.Writes or EdgeRelation.Calls;

    private static string ToRelationString(EdgeRelation relation) => relation switch
    {
        EdgeRelation.Calls => "CALLS",
        EdgeRelation.Imports => "IMPORTS",
        EdgeRelation.Uses => "USES",
        EdgeRelation.Reads => "READS",
        EdgeRelation.Writes => "WRITES",
        EdgeRelation.Executes => "EXECUTES",
        EdgeRelation.DependsOn => "DEPENDS_ON",
        EdgeRelation.Owns => "OWNS",
        EdgeRelation.MigratesTo => "MIGRATES_TO",
        _ => relation.ToString().ToUpperInvariant()
    };
}
