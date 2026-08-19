using FindingsMigration.Contracts;
using FindingsMigration.Core;
using Xunit;

namespace FindingsMigration.Tests;

public class SpHierarchyAnalyzerTests
{
    [Fact]
    public void Analyze_from_inventory_builds_procedure_tree_and_column_usage()
    {
        // Arrange
        var analyzer = new SpHierarchyAnalyzer();
        var spMap = new StoredProcedureMapDocument(); // empty => no map validation required

        var inventoryJson = """
            {
              "generatedAt": "2026-08-19T00:00:00Z",
              "rootProcedure": "dbo.usp_Root",
              "procedureEdges": [
                { "ParentProcedureFqn": "dbo.usp_Root", "ChildProcedureFqn": "dbo.usp_Child", "ChildDepth": 1 }
              ],
              "tableColumnUsage": [
                { "ProcedureFqn": "dbo.usp_Root", "TableFqn": "dbo.Customer", "ColumnName": "Id", "IsUsed": true },
                { "ProcedureFqn": "dbo.usp_Root", "TableFqn": "dbo.Customer", "ColumnName": "Name", "IsUsed": true },
                { "ProcedureFqn": "dbo.usp_Root", "TableFqn": "dbo.Customer", "ColumnName": "Email", "IsUsed": false },

                { "ProcedureFqn": "dbo.usp_Child", "TableFqn": "dbo.CustomerAddress", "ColumnName": "CustomerId", "IsUsed": true },
                { "ProcedureFqn": "dbo.usp_Child", "TableFqn": "dbo.CustomerAddress", "ColumnName": "AddressLine1", "IsUsed": false }
              ],
              "typeDependencies": [
                { "ProcedureFqn": "dbo.usp_Root", "TypeFqn": "dbo.CustomerIdList" }
              ],
              "viewDependencies": [
                { "ProcedureFqn": "dbo.usp_Root", "ViewFqn": "dbo.vw_ActiveCustomers" }
              ]
            }
            """;

        // Act
        var result = analyzer.Analyze(spMap, "dbo.usp_Root", inventoryJson);

        // Assert
        Assert.Equal("dbo.usp_Root", result.RootProcedure);
        Assert.NotNull(result.Dependencies);
        Assert.Equal(4, result.Dependencies.Count);

        var childProc = Assert.Single(result.Dependencies, n => n.Kind == "PROCEDURE");
        Assert.Equal("dbo", childProc.Schema);
        Assert.Equal("usp_Child", childProc.Name);
        Assert.Equal(1, childProc.Depth);

        var rootTable = Assert.Single(result.Dependencies, n => n.Kind == "TABLE" && n.Name == "Customer");
        Assert.NotNull(rootTable.ColumnUsage);
        Assert.Equal(3, rootTable.ColumnUsage!.TotalColumns);
        Assert.Equal(2, rootTable.ColumnUsage.UsedColumns);
        Assert.Contains("Id", rootTable.ColumnUsage.Used);
        Assert.Contains("Name", rootTable.ColumnUsage.Used);
        Assert.Contains("Email", rootTable.ColumnUsage.Unused);

        var typeNode = Assert.Single(result.Dependencies, n => n.Kind == "TYPE");
        Assert.Equal("dbo", typeNode.Schema);
        Assert.Equal("CustomerIdList", typeNode.Name);
        Assert.Equal(1, typeNode.Depth);

        var viewNode = Assert.Single(result.Dependencies, n => n.Kind == "VIEW");
        Assert.Equal("dbo", viewNode.Schema);
        Assert.Equal("vw_ActiveCustomers", viewNode.Name);
        Assert.Equal(1, viewNode.Depth);

        // Child procedure should contain its own table node + column usage.
        var childTable = Assert.Single(childProc.Children, n => n.Kind == "TABLE" && n.Name == "CustomerAddress");
        Assert.NotNull(childTable.ColumnUsage);
        Assert.Equal(2, childTable.ColumnUsage!.TotalColumns);
        Assert.Equal(1, childTable.ColumnUsage.UsedColumns);
        Assert.Contains("CustomerId", childTable.ColumnUsage.Used);
        Assert.Contains("AddressLine1", childTable.ColumnUsage.Unused);
    }

    [Fact]
    public void Analyze_from_inventory_includes_function_dependencies()
    {
        var analyzer = new SpHierarchyAnalyzer();
        var spMap = new StoredProcedureMapDocument();

        var inventoryJson = """
            {
              "rootProcedure": "dbo.usp_Root",
              "procedureEdges": [],
              "tableColumnUsage": [
                { "ProcedureFqn": "dbo.usp_Root", "TableFqn": "dbo.Orders", "ColumnName": "Id", "IsUsed": true }
              ],
              "typeDependencies": [],
              "viewDependencies": [],
              "functionDependencies": [
                { "ProcedureFqn": "dbo.usp_Root", "FunctionFqn": "dbo.fn_GetDiscount", "FunctionType": "SQL_SCALAR_FUNCTION" }
              ]
            }
            """;

        var result = analyzer.Analyze(spMap, "dbo.usp_Root", inventoryJson);

        Assert.Equal(2, result.Dependencies.Count);
        var funcNode = Assert.Single(result.Dependencies, n => n.Kind == "FUNCTION");
        Assert.Equal("fn_GetDiscount", funcNode.Name);
    }

    [Fact]
    public void Analyze_detects_cycle_and_does_not_infinite_loop()
    {
        var analyzer = new SpHierarchyAnalyzer();
        var spMap = new StoredProcedureMapDocument();

        var inventoryJson = """
            {
              "rootProcedure": "dbo.usp_A",
              "procedureEdges": [
                { "ParentProcedureFqn": "dbo.usp_A", "ChildProcedureFqn": "dbo.usp_B", "ChildDepth": 1 },
                { "ParentProcedureFqn": "dbo.usp_B", "ChildProcedureFqn": "dbo.usp_A", "ChildDepth": 2 }
              ],
              "tableColumnUsage": [],
              "typeDependencies": [],
              "viewDependencies": [],
              "functionDependencies": []
            }
            """;

        var result = analyzer.Analyze(spMap, "dbo.usp_A", inventoryJson);

        Assert.Equal("dbo.usp_A", result.RootProcedure);
        var childB = Assert.Single(result.Dependencies, n => n.Kind == "PROCEDURE" && n.Name == "usp_B");
        // usp_B tries to recurse back to usp_A — should produce a leaf, not infinite recursion
        var cycleLeaf = Assert.Single(childB.Children, n => n.Name == "usp_A");
        Assert.Empty(cycleLeaf.Children);
    }

    [Fact]
    public void Analyze_from_sp_map_only_returns_tables_from_reads_writes()
    {
        var analyzer = new SpHierarchyAnalyzer();
        var spMap = new StoredProcedureMapDocument
        {
            Procedures =
            [
                new StoredProcedureEntry
                {
                    Name = "usp_GetOrder", Schema = "dbo",
                    Reads = ["dbo.Orders", "dbo.OrderItems"],
                    Writes = ["dbo.AuditLog"]
                }
            ]
        };

        var result = analyzer.Analyze(spMap, "dbo.usp_GetOrder");

        Assert.Equal("dbo.usp_GetOrder", result.RootProcedure);
        Assert.Equal(3, result.Dependencies.Count);
        Assert.All(result.Dependencies, d => Assert.Equal("TABLE", d.Kind));
        Assert.Contains(result.Dependencies, d => d.Name == "AuditLog");
        Assert.Contains(result.Dependencies, d => d.Name == "Orders");
        Assert.Contains(result.Dependencies, d => d.Name == "OrderItems");
    }

    [Fact]
    public void Analyze_throws_when_root_sp_not_in_map()
    {
        var analyzer = new SpHierarchyAnalyzer();
        var spMap = new StoredProcedureMapDocument
        {
            Procedures = [new StoredProcedureEntry { Name = "usp_Other", Schema = "dbo" }]
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            analyzer.Analyze(spMap, "dbo.usp_Missing"));

        Assert.Contains("usp_Missing", ex.Message);
    }

    [Fact]
    public void Normalize_handles_bracket_quoted_fqns()
    {
        var analyzer = new SpHierarchyAnalyzer();
        var spMap = new StoredProcedureMapDocument();

        var inventoryJson = """
            {
              "rootProcedure": "[dbo].[usp_Root]",
              "procedureEdges": [],
              "tableColumnUsage": [
                { "ProcedureFqn": "[dbo].[usp_Root]", "TableFqn": "[dbo].[My Table]", "ColumnName": "Id", "IsUsed": true }
              ],
              "typeDependencies": [],
              "viewDependencies": [],
              "functionDependencies": []
            }
            """;

        var result = analyzer.Analyze(spMap, "[dbo].[usp_Root]", inventoryJson);

        Assert.Equal("dbo.usp_Root", result.RootProcedure);
        var table = Assert.Single(result.Dependencies);
        Assert.Equal("My Table", table.Name);
    }

    [Fact]
    public void RenderTree_produces_tree_output()
    {
        var analyzer = new SpHierarchyAnalyzer();
        var result = new SpHierarchyResult
        {
            RootProcedure = "dbo.usp_Root",
            Dependencies =
            [
                new SpDependencyNode
                {
                    Kind = "TABLE", Schema = "dbo", Name = "Customer", Depth = 1,
                    ColumnUsage = new TableColumnUsage
                    {
                        TotalColumns = 3, UsedColumns = 2,
                        Used = ["Id", "Name"], Unused = ["Email"]
                    }
                },
                new SpDependencyNode
                {
                    Kind = "PROCEDURE", Schema = "dbo", Name = "usp_Child", Depth = 1,
                    Children =
                    [
                        new SpDependencyNode { Kind = "TABLE", Schema = "dbo", Name = "Orders", Depth = 2 }
                    ]
                }
            ]
        };

        var tree = analyzer.RenderTree(result);

        Assert.Contains("Root: dbo.usp_Root", tree);
        Assert.Contains("[TABLE] dbo.Customer (2/3 cols used)", tree);
        Assert.Contains("used: Id, Name", tree);
        Assert.Contains("unused: Email", tree);
        Assert.Contains("[PROCEDURE] dbo.usp_Child", tree);
        Assert.Contains("[TABLE] dbo.Orders", tree);
        Assert.Contains("├", tree);
        Assert.Contains("└", tree);
    }
}

