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
        var map = new CodeToDbMapDto { GeneratedAt = DateTimeOffset.UtcNow };
        foreach (var edge in graph.Edges.Where(IsCodeToDbEdge))
        {
            var code = graph.FindNode(edge.Source);
            var db = graph.FindNode(edge.Target);
            if (code is null || db is null)
                continue;

            map.Entries.Add(new CodeToDbEntryDto
            {
                CodeNodeId = code.Id,
                CodeLabel = code.Label,
                SourceFile = edge.Evidence?.File ?? code.SourceFile,
                Line = edge.Evidence?.Line,
                DbNodeId = db.Id,
                DbObject = db.Label,
                DbKind = db.Kind.ToString(),
                Relation = ToRelationString(edge.Relation),
                Confidence = edge.Confidence.ToString().ToUpperInvariant(),
                Pattern = edge.Evidence?.Pattern,
                Project = code.Properties.GetValueOrDefault(ProjectGraphIds.ProjectPropertyKey)
                          ?? ProjectGraphIds.TryGetProject(code.Id)
            });
        }

        return map;
    }

    public StoredProcedureMapDto ToStoredProcedureMap(EvidenceGraph graph)
    {
        var dto = new StoredProcedureMapDto { GeneratedAt = DateTimeOffset.UtcNow };
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
                    entry.CodeCallers.Add(caller.Label);
                else
                    entry.SqlCallers.Add(caller.Label);
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

            dto.Procedures.Add(entry);
        }

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
