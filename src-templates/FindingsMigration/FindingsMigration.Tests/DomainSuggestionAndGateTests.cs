using FindingsMigration.Core;
using Xunit;

namespace FindingsMigration.Tests;

public sealed class DomainSuggestionServiceTests
{
    [Fact]
    public void Suggest_clusters_path_and_community()
    {
        var graph = new GraphDocument
        {
            Nodes =
            [
                new GraphNodeLite { Id = "1", Kind = "Method", Community = "1", SourceFile = @"src\Features\Billing\Pay.cs" },
                new GraphNodeLite { Id = "2", Kind = "Method", Community = "1", SourceFile = @"src\Features\Billing\Invoice.cs" },
                new GraphNodeLite { Id = "3", Kind = "Method", Community = "1", SourceFile = @"src\Features\Billing\Refund.cs" },
                new GraphNodeLite { Id = "4", Kind = "Type", Community = "2", SourceFile = @"src\Features\Onboarding\Start.cs" },
                new GraphNodeLite { Id = "5", Kind = "Type", Community = "2", SourceFile = @"src\Features\Onboarding\Verify.cs" },
                new GraphNodeLite { Id = "6", Kind = "Type", Community = "2", SourceFile = @"src\Features\Onboarding\Finish.cs" }
            ]
        };

        var result = new DomainSuggestionService().Suggest(graph, minNodesPerDomain: 3);
        Assert.True(result.SuggestionCount >= 2);
        Assert.Contains(result.Suggestions, s => s.ProposedDomain.Contains("Billing", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Suggestions, s => s.ProposedDomain.Contains("Onboarding", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class ConfidenceGateServiceTests
{
    [Fact]
    public void Gate_fails_when_extracted_missing_from_manifests()
    {
        var dir = Path.Combine(Path.GetTempPath(), "fm-gate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var mapPath = Path.Combine(dir, "code-to-db-map.json");
            File.WriteAllText(mapPath, """
                {
                  "entries": [
                    { "codeLabel": "X.Y", "dbObject": "showcase.Items", "confidence": "EXTRACTED" }
                  ]
                }
                """);
            var manifests = Path.Combine(dir, "manifests");
            Directory.CreateDirectory(manifests);
            File.WriteAllText(Path.Combine(manifests, "empty.yml"), "domain: other\nowns: []\n");

            var result = new ConfidenceGateService().Evaluate(mapPath, manifests, ownedSchema: "showcase");
            Assert.False(result.Passed);
            Assert.Contains(result.Failures, f => f.Contains("showcase.Items", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
