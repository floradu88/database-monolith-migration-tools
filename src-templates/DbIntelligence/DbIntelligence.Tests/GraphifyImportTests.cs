using System.Text.Json;
using DbIntelligence.Contracts;
using DbIntelligence.Infrastructure;
using DbIntelligence.Infrastructure.Graphify;
using Microsoft.Extensions.Options;
using Xunit;

namespace DbIntelligence.Tests;

public class GraphifyImportTests
{
    [Fact]
    public async Task Import_accepts_graphify_cli_links_and_numeric_community()
    {
        var root = Path.Combine(Path.GetTempPath(), "dbintel-graphify-" + Guid.NewGuid().ToString("N"));
        var outDir = Path.Combine(root, "graphify-out");
        Directory.CreateDirectory(outDir);

        File.WriteAllText(Path.Combine(outDir, "graph.json"), """
            {
              "directed": true,
              "multigraph": false,
              "graph": {},
              "nodes": [
                {
                  "id": "src_customerrepository",
                  "label": "CustomerRepository.cs",
                  "file_type": "code",
                  "source_file": "src/CustomerRepository.cs",
                  "source_location": "L1",
                  "community": 0
                },
                {
                  "id": "src_customerrepository_sample_customerrepository",
                  "label": "CustomerRepository",
                  "type": "class",
                  "source_file": "src/CustomerRepository.cs",
                  "source_location": "L4",
                  "community": 0
                }
              ],
              "links": [
                {
                  "relation": "contains",
                  "confidence": "EXTRACTED",
                  "source_file": "src/CustomerRepository.cs",
                  "source_location": "L4",
                  "weight": 1.0,
                  "source": "src_customerrepository",
                  "target": "src_customerrepository_sample_customerrepository"
                }
              ]
            }
            """);

        try
        {
            var client = new GraphifyClient(
                new CliProcessRunner(),
                Options.Create(new DbIntelligenceOptions()));

            var graph = await client.ImportGraphJsonAsync(root);
            Assert.NotNull(graph);
            Assert.Equal(2, graph!.Nodes.Count);
            Assert.Single(graph.Edges);
            Assert.Equal("0", graph.Nodes.First().Community);
            Assert.Contains(graph.Nodes, n => n.Kind == Domain.NodeKind.File || n.Kind == Domain.NodeKind.Type);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Deserialize_graphify_document_maps_links()
    {
        var json = """
            {"nodes":[{"id":"a","label":"A","community":1}],"links":[{"source":"a","target":"b","relation":"contains","confidence":"EXTRACTED"}]}
            """;
        var dto = JsonSerializer.Deserialize<GraphifyGraphDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(dto);
        Assert.Equal("1", dto!.Nodes[0].Community);
        Assert.Single(dto.AllEdges);
        Assert.Equal("a", dto.AllEdges[0].FromId);
    }
}
