using DbIntelligence.Domain;
using DbIntelligence.Infrastructure;
using DbIntelligence.RepositoryScanner;
using DbIntelligence.SqlScanner;
using Xunit;

namespace DbIntelligence.Tests;

public class RepositoryScannerTests
{
    [Fact]
    public async Task Scan_detects_stored_procedure_and_sql_patterns()
    {
        var root = CreateFixtureRepo();
        try
        {
            var scanner = new RepositoryScannerService();
            var findings = await scanner.ScanAsync(root);

            Assert.Contains(findings, f =>
                f.NormalizedObjectName.Contains("usp_Customer_Get", StringComparison.OrdinalIgnoreCase) &&
                f.AccessType == EdgeRelation.Executes);

            Assert.Contains(findings, f =>
                f.NormalizedObjectName.Contains("Customer", StringComparison.OrdinalIgnoreCase) &&
                f.AccessType == EdgeRelation.Reads);

            var graph = scanner.ToGraph(findings, database: "Monolith");
            Assert.NotEmpty(graph.Nodes);
            Assert.NotEmpty(graph.Edges);

            var merger = new EvidenceGraphMerger();
            var map = merger.ToCodeToDbMap(graph);
            Assert.NotEmpty(map.Entries);
            Assert.NotEmpty(map.References);
            Assert.All(map.References, r =>
            {
                Assert.False(string.IsNullOrWhiteSpace(r.FullPath));
                Assert.Contains(':', r.Location);
                Assert.True(r.Line is > 0);
            });
            Assert.Contains(map.References, r =>
                r.FullPath.Contains("CustomerRepository.cs", StringComparison.OrdinalIgnoreCase));

            var locationsDoc = merger.ToCodeReferenceLocations(graph);
            Assert.Equal(map.References.Count, locationsDoc.Count);
            Assert.Equal(graph.Meta.TargetRepositoryPath, locationsDoc.RepositoryPath);

            var spMap = merger.ToStoredProcedureMap(graph);
            Assert.Contains(spMap.Procedures, p => p.Name.Contains("usp_Customer_Get", StringComparison.OrdinalIgnoreCase));
            Assert.NotEmpty(spMap.References);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateFixtureRepo()
    {
        var root = Path.Combine(Path.GetTempPath(), "dbintel-fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "src", "CustomerRepository.cs"), """
            using System.Data;
            using System.Data.SqlClient;

            namespace Sample;

            public class CustomerRepository
            {
                public Customer GetById(int id)
                {
                    using var connection = new SqlConnection("Server=.;Database=Monolith;Trusted_Connection=True;");
                    using var command = new SqlCommand("usp_Customer_Get", connection)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    command.Parameters.AddWithValue("@Id", id);
                    connection.Open();
                    command.ExecuteReader();
                    return new Customer();
                }

                public IEnumerable<Customer> Search(string term)
                {
                    const string sql = "SELECT Id, Name FROM dbo.Customer WHERE Name LIKE @term";
                    // Dapper-style call site for scanner
                    QueryAsync<Customer>(sql);
                    return Array.Empty<Customer>();
                }

                private static void QueryAsync<T>(string sql) { }
            }

            public class Customer
            {
                public int Id { get; set; }
                public string Name { get; set; } = string.Empty;
            }
            """);

        File.WriteAllText(Path.Combine(root, "src", "seed.sql"), """
            EXEC dbo.usp_Customer_Get;
            SELECT * FROM dbo.Customer;
            """);

        return root;
    }
}

public class SqlScannerAndMergerTests
{
    [Fact]
    public void SqlScanner_from_inventory_builds_dependency_edges()
    {
        var scanner = new SqlScannerService();
        var graph = scanner.FromInventory(
            [
                new SqlObjectRecord("Monolith", "dbo", "usp_Customer_Get", "SQL_STORED_PROCEDURE"),
                new SqlObjectRecord("Monolith", "dbo", "Customer", "USER_TABLE")
            ],
            [
                new SqlDependencyRecord("Monolith", "dbo", "usp_Customer_Get", "SQL_STORED_PROCEDURE", "dbo", "Customer", false)
            ]);

        Assert.Equal(2, graph.Nodes.Count);
        Assert.Single(graph.Edges);

        var merger = new EvidenceGraphMerger();
        var spMap = merger.ToStoredProcedureMap(graph);
        Assert.Single(spMap.Procedures);
        Assert.Contains(spMap.Procedures[0].Writes, w => w.Contains("Customer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Merger_exports_graphify_shaped_json_model()
    {
        var graph = new EvidenceGraph();
        graph.Meta.Sources.Add("test");
        graph.UpsertNode(new GraphNode { Id = "code:A.B", Label = "A.B", Kind = NodeKind.Method });
        graph.UpsertNode(new GraphNode { Id = "db:default.dbo.usp_X:StoredProcedure", Label = "dbo.usp_X", Kind = NodeKind.StoredProcedure, Schema = "dbo" });
        graph.UpsertEdge(new GraphEdge
        {
            Source = "code:A.B",
            Target = "db:default.dbo.usp_X:StoredProcedure",
            Relation = EdgeRelation.Executes,
            Confidence = Confidence.Extracted
        });

        var merger = new EvidenceGraphMerger();
        var dto = merger.ToGraphifyDto(graph);
        Assert.Equal(2, dto.Nodes.Count);
        Assert.Single(dto.Edges);
        Assert.Equal("EXECUTES", dto.Edges[0].Relation);
        Assert.Equal("EXTRACTED", dto.Edges[0].Confidence);
    }
}
