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
}

