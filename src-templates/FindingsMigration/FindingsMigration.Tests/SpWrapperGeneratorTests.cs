using FindingsMigration.Contracts;
using FindingsMigration.Core;
using Xunit;

namespace FindingsMigration.Tests;

public class SpWrapperGeneratorTests
{
    [Fact]
    public void Generate_emits_sql_stub_and_csharp_wrapper()
    {
        var root = Path.Combine(Path.GetTempPath(), "sp-gen-" + Guid.NewGuid().ToString("N"));
        var service = "InsightDataService";
        var serviceRoot = Path.Combine(root, service);
        Directory.CreateDirectory(Path.Combine(serviceRoot, $"{service}.Database"));
        Directory.CreateDirectory(Path.Combine(serviceRoot, $"{service}.Infrastructure"));

        try
        {
            var map = new StoredProcedureMapDocument
            {
                Procedures =
                [
                    new StoredProcedureEntry
                    {
                        Name = "GetInsight",
                        Schema = "dbo",
                        Callers = ["InsightApp.Get"],
                        Reads = ["dbo.Insight"],
                        Writes = []
                    }
                ]
            };

            var result = new SpWrapperGenerator().Generate(map, serviceRoot, "Insight", "insight", service);
            Assert.Equal(1, result.ProcedureCount);
            Assert.Contains(result.WrittenFiles, f => f.EndsWith("GetInsight.sql", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result.WrittenFiles, f => f.EndsWith("Sp_GetInsight.cs", StringComparison.OrdinalIgnoreCase));
            var cs = File.ReadAllText(result.WrittenFiles.First(f => f.EndsWith(".cs")));
            Assert.Contains("insight.GetInsight", cs);
            Assert.Contains("ExecuteSp<object>", cs);
            Assert.Contains("IDataAccessContext", cs);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
