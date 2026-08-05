using FindingsMigration.Contracts;
using FindingsMigration.Core;
using Xunit;

namespace FindingsMigration.Tests;

public class DomainPackageBuilderTests
{
    [Fact]
    public void Build_packages_extracted_and_holds_ambiguous()
    {
        var root = Path.Combine(Path.GetTempPath(), "findings-mig-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var mapPath = Path.Combine(root, "code-to-db-map.json");
        File.WriteAllText(mapPath, """
            {
              "generatedAt": "2026-07-31T00:00:00Z",
              "entries": [
                {
                  "codeNodeId": "code:A",
                  "codeLabel": "A.Get",
                  "sourceFile": "src/A.cs",
                  "line": 10,
                  "dbNodeId": "db:dbo.Customer:Table",
                  "dbObject": "dbo.Customer",
                  "dbKind": "Table",
                  "relation": "READS",
                  "confidence": "EXTRACTED",
                  "pattern": "ef-linq"
                },
                {
                  "codeNodeId": "code:B",
                  "codeLabel": "B.Dyn",
                  "sourceFile": "src/B.cs",
                  "line": 20,
                  "dbNodeId": "db:dbo.X:Table",
                  "dbObject": "dbo.X",
                  "dbKind": "Table",
                  "relation": "READS",
                  "confidence": "AMBIGUOUS",
                  "pattern": "interpolated-sql"
                }
              ]
            }
            """);

        var outDir = Path.Combine(root, "out");
        try
        {
            var result = new DomainPackageBuilder().Build(
                mapPath,
                null,
                outDir,
                new DomainPackageOptions
                {
                    DomainName = "Customer",
                    TargetService = "CustomerDataService",
                    IncludeAmbiguous = false
                });

            Assert.Equal(1, result.ExtractedCount);
            Assert.Equal(1, result.AmbiguousCount);
            Assert.Equal(1, result.SkippedAmbiguousCount);
            Assert.True(File.Exists(Path.Combine(outDir, "manifests", "domains", "customer.from-findings.yml")));
            Assert.True(File.Exists(Path.Combine(outDir, "FINDINGS-REVIEW.md")));
            Assert.True(File.Exists(Path.Combine(outDir, "domain-package.json")));
            Assert.True(File.Exists(Path.Combine(outDir, "api-stubs", "API-STUBS.md")));
            var review = File.ReadAllText(Path.Combine(outDir, "FINDINGS-REVIEW.md"));
            Assert.Contains("dbo.X", review);
            Assert.Contains("Data access hints", review);
            Assert.Contains("EF Core", review);
            var apiStub = File.ReadAllText(Path.Combine(outDir, "api-stubs", "API-STUBS.md"));
            Assert.Contains("Recommend:", apiStub);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Build_emit_reconciliation_tests_writes_stub()
    {
        var root = Path.Combine(Path.GetTempPath(), "findings-recon-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var mapPath = Path.Combine(root, "code-to-db-map.json");
        File.WriteAllText(mapPath, """
            {"entries":[{"codeLabel":"A.Get","dbObject":"dbo.Customer","dbKind":"Table","relation":"READS","confidence":"EXTRACTED","pattern":"ef-linq"}]}
            """);
        var outDir = Path.Combine(root, "out");
        try
        {
            var result = new DomainPackageBuilder().Build(
                mapPath,
                null,
                outDir,
                new DomainPackageOptions
                {
                    DomainName = "Customer",
                    TargetService = "CustomerDataService",
                    EmitReconciliationTests = true
                });

            Assert.Contains(result.WrittenFiles, f => f.Contains("ShadowReconciliationStubTests.cs", StringComparison.OrdinalIgnoreCase));
            Assert.True(File.Exists(Path.Combine(outDir, "Tests", "CustomerShadowReconciliationStubTests.cs")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
