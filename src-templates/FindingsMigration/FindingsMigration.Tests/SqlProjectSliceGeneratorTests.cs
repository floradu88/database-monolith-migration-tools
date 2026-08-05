using FindingsMigration.Core;
using Xunit;

namespace FindingsMigration.Tests;

public class SqlProjectSliceGeneratorTests
{
    [Fact]
    public void Generate_writes_stub_sql_and_ownership_yml()
    {
        var outDir = Path.Combine(Path.GetTempPath(), "slice-sql-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = new SqlProjectSliceGenerator().GenerateFromCommaList(
                "dbo.Customer,dbo.Order",
                outDir,
                "customer",
                "CustomerDataService",
                "Customer Platform");

            Assert.Equal(2, result.ObjectCount);
            Assert.Contains(result.WrittenFiles, f => f.EndsWith("ownership.yml", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result.WrittenFiles, f => f.EndsWith("customer.sql", StringComparison.OrdinalIgnoreCase));
            var sql = File.ReadAllText(result.WrittenFiles.First(f => f.EndsWith("customer.sql", StringComparison.OrdinalIgnoreCase)));
            Assert.Contains("DefinitionHashPlaceholder:", sql);
            Assert.Contains("Ownership: CustomerDataService", sql);
            Assert.DoesNotContain("CREATE TABLE", sql, StringComparison.OrdinalIgnoreCase);
            var yml = File.ReadAllText(Path.Combine(outDir, "ownership.yml"));
            Assert.Contains("dbo.Customer", yml);
            Assert.Contains("requires_human_ownership_approval: true", yml);
        }
        finally
        {
            if (Directory.Exists(outDir))
                Directory.Delete(outDir, recursive: true);
        }
    }
}
