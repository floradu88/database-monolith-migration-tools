using DbIntelligence.Contracts;
using DbIntelligence.Domain;
using DbIntelligence.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DbIntelligence.Tests;

public class CombinedGraphServiceTests
{
    [Fact]
    public async Task CombineFromParent_MergesProjectGraphs_WithSharedDbNodes()
    {
        var root = Path.Combine(Path.GetTempPath(), "dbi-combine-" + Guid.NewGuid().ToString("N"));
        var a = Path.Combine(root, "AppA");
        var b = Path.Combine(root, "AppB");
        Directory.CreateDirectory(Path.Combine(a, ".db-index"));
        Directory.CreateDirectory(Path.Combine(b, ".db-index"));

        try
        {
            await File.WriteAllTextAsync(Path.Combine(a, ".db-index", "graph.json"), """
                {
                  "nodes": [
                    { "id": "code:A.Run", "label": "A.Run", "kind": "Method" },
                    { "id": "db:default.dbo.Customers:Table", "label": "dbo.Customers", "kind": "Table", "schema": "dbo" }
                  ],
                  "edges": [
                    { "source": "code:A.Run", "target": "db:default.dbo.Customers:Table", "relation": "READS", "confidence": "EXTRACTED" }
                  ],
                  "meta": { "sources": ["test-a"] }
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(b, ".db-index", "graph.json"), """
                {
                  "nodes": [
                    { "id": "code:B.Run", "label": "B.Run", "kind": "Method" },
                    { "id": "db:default.dbo.Customers:Table", "label": "dbo.Customers", "kind": "Table", "schema": "dbo" }
                  ],
                  "edges": [
                    { "source": "code:B.Run", "target": "db:default.dbo.Customers:Table", "relation": "WRITES", "confidence": "EXTRACTED" }
                  ],
                  "meta": { "sources": ["test-b"] }
                }
                """);

            var merger = new EvidenceGraphMerger();
            var store = new FileIntelligenceStore(
                Options.Create(new DbIntelligenceOptions()),
                merger);
            var svc = new CombinedGraphService(store, merger, NullLogger<CombinedGraphService>.Instance);

            var result = await svc.CombineFromParentAsync(new CombineGraphsRequest
            {
                ParentFolderPath = root,
                ExportCombined = true,
                ShareDatabaseNodes = true
            });

            Assert.Equal(2, result.ProjectsLoaded);
            Assert.NotNull(store.CurrentGraph);
            Assert.Contains(store.CurrentGraph!.Nodes, n => n.Id == "db:default.dbo.Customers:Table");
            Assert.Contains(store.CurrentGraph.Nodes, n => n.Id.StartsWith("p:AppA/", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(store.CurrentGraph.Nodes, n => n.Id.StartsWith("p:AppB/", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(1, store.CurrentGraph.Nodes.Count(n => n.Id.StartsWith("db:", StringComparison.OrdinalIgnoreCase)));

            var map = merger.ToCodeToDbMap(store.CurrentGraph);
            Assert.Equal(2, map.Entries.Count);
            Assert.Contains(map.Entries, e => e.Project == "AppA");
            Assert.Contains(map.Entries, e => e.Project == "AppB");
            Assert.True(Directory.Exists(Path.Combine(root, ".db-index-combined")));
            Assert.True(File.Exists(Path.Combine(root, ".db-index-combined", "graph.json")));
            Assert.True(File.Exists(Path.Combine(root, ".db-index-combined", "findings.html")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
