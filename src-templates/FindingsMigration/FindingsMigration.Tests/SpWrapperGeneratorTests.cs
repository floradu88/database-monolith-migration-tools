using FindingsMigration.Contracts;
using FindingsMigration.Core;
using Xunit;

namespace FindingsMigration.Tests;

public class SpWrapperGeneratorTests
{
    [Fact]
    public void Generate_expands_templated_procedure_names()
    {
        var root = Path.Combine(Path.GetTempPath(), "sp-gen-tmpl-" + Guid.NewGuid().ToString("N"));
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
                        Name = "usp_{Area}_{Action}",
                        NameTemplate = "usp_{Area}_{Action}",
                        Tokens = new Dictionary<string, List<string>>
                        {
                            ["Area"] = ["Billing", "Ordering"],
                            ["Action"] = ["Get"]
                        },
                        Callers = ["InsightApp.Run"]
                    }
                ]
            };

            var result = new SpWrapperGenerator().Generate(map, serviceRoot, "Insight", "insight", service);
            Assert.Contains(result.WrittenFiles, f => f.EndsWith("usp_Billing_Get.sql", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result.WrittenFiles, f => f.EndsWith("usp_Ordering_Get.sql", StringComparison.OrdinalIgnoreCase));
            var wrapper = File.ReadAllText(result.WrittenFiles.First(f => f.EndsWith(".cs")));
            Assert.Contains("NameTemplate", wrapper);
            Assert.Contains("StoredProcedureName.Format", wrapper);
            Assert.Contains("usp_{Area}_{Action}", wrapper);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Generate_registers_sql_stub_in_sqlproj_when_present()
    {
        var root = Path.Combine(Path.GetTempPath(), "sp-gen-proj-" + Guid.NewGuid().ToString("N"));
        var service = "InsightDataService";
        var serviceRoot = Path.Combine(root, service);
        var dbDir = Path.Combine(serviceRoot, $"{service}.Database");
        Directory.CreateDirectory(dbDir);
        Directory.CreateDirectory(Path.Combine(serviceRoot, $"{service}.Infrastructure"));
        File.WriteAllText(Path.Combine(dbDir, $"{service}.Database.sqlproj"), """
            <?xml version="1.0" encoding="utf-8"?>
            <Project>
              <ItemGroup>
                <Build Include="Programmability\Existing.sql" />
              </ItemGroup>
            </Project>
            """);

        try
        {
            var map = new StoredProcedureMapDocument
            {
                Procedures = [new StoredProcedureEntry { Name = "GetInsight", Schema = "dbo" }]
            };
            new SpWrapperGenerator().Generate(map, serviceRoot, "Insight", "insight", service);
            var sqlproj = File.ReadAllText(Path.Combine(dbDir, $"{service}.Database.sqlproj"));
            Assert.Contains(@"Build Include=""Programmability\Generated\GetInsight.sql""", sqlproj);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

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
            var sql = File.ReadAllText(result.WrittenFiles.First(f => f.EndsWith(".sql")));
            Assert.Contains("Ownership: SqlProject", sql);
            Assert.Contains("Cutover/", sql);
            var cs = File.ReadAllText(result.WrittenFiles.First(f => f.EndsWith(".cs")));
            Assert.Contains("insight.GetInsight", cs);
            Assert.Contains("ExecuteSp<object>", cs);
            Assert.Contains("IDataAccessContext", cs);
            Assert.Contains(result.WrittenFiles, f => f.EndsWith("GetInsight.migration-manifest.snippet.yml", StringComparison.OrdinalIgnoreCase));
            var snippet = File.ReadAllText(result.WrittenFiles.First(f => f.EndsWith(".migration-manifest.snippet.yml")));
            Assert.Contains("type: StoredProcedure", snippet);
            Assert.Contains("targetService: InsightDataService", snippet);
            Assert.Contains("wave: insight-001", snippet);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
