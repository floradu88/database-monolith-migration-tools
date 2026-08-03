using DbIntelligence.Domain;
using DbIntelligence.Infrastructure;
using Microsoft.Extensions.Options;
using Xunit;

namespace DbIntelligence.Tests;

public class FileIntelligenceStoreExportTests
{
    [Fact]
    public async Task ExportAsync_writes_json_markdown_and_html_under_output_directory()
    {
        var root = Path.Combine(Path.GetTempPath(), "dbi-export-" + Guid.NewGuid().ToString("N"));
        var outDir = Path.Combine(root, ".db-index");
        Directory.CreateDirectory(root);

        try
        {
            var graph = new EvidenceGraph();
            graph.Meta.TargetRepositoryPath = root;
            graph.Meta.Sources.Add("test");
            graph.Nodes.Add(new GraphNode
            {
                Id = "code:App.Run",
                Label = "App.Run",
                Kind = NodeKind.Method,
                SourceFile = Path.Combine(root, "App.cs"),
                SourceLocation = "L10"
            });
            graph.Nodes.Add(new GraphNode
            {
                Id = "db:default.dbo.Customers:Table",
                Label = "dbo.Customers",
                Kind = NodeKind.Table,
                Schema = "dbo"
            });
            graph.Edges.Add(new GraphEdge
            {
                Source = "code:App.Run",
                Target = "db:default.dbo.Customers:Table",
                Relation = EdgeRelation.Reads,
                Confidence = Confidence.Extracted,
                Evidence = new EdgeEvidence
                {
                    File = Path.Combine(root, "App.cs"),
                    Line = 10,
                    Pattern = "test"
                }
            });

            var store = new FileIntelligenceStore(
                Options.Create(new DbIntelligenceOptions()),
                new EvidenceGraphMerger());

            await store.ExportAsync(graph, outDir);

            Assert.True(File.Exists(Path.Combine(outDir, "graph.json")));
            Assert.True(File.Exists(Path.Combine(outDir, "code-to-db-map.json")));
            Assert.True(File.Exists(Path.Combine(outDir, "stored-procedure-map.json")));
            Assert.True(File.Exists(Path.Combine(outDir, "code-reference-locations.json")));
            Assert.True(File.Exists(Path.Combine(outDir, "GRAPH_REPORT.md")));
            Assert.True(File.Exists(Path.Combine(outDir, "findings.html")));

            var md = await File.ReadAllTextAsync(Path.Combine(outDir, "GRAPH_REPORT.md"));
            Assert.Contains("```mermaid", md, StringComparison.Ordinal);
            Assert.Contains("flowchart", md, StringComparison.Ordinal);

            var html = await File.ReadAllTextAsync(Path.Combine(outDir, "findings.html"));
            Assert.Contains("DbIntelligence findings", html, StringComparison.Ordinal);
            Assert.Contains("dbo.Customers", html, StringComparison.Ordinal);
            Assert.Contains("class=\"mermaid\"", html, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Default_artifacts_directory_is_db_index()
    {
        Assert.Equal(".db-index", DbIntelligenceOptions.DefaultArtifactsDirectory);
        Assert.Equal(".db-index", new DbIntelligenceOptions().ArtifactsDirectory);
        Assert.Equal(".db-index-combined", DbIntelligenceOptions.DefaultCombinedDirectoryName);
    }
}
