using ShowcaseDataService.Infrastructure;
using Xunit;

namespace ShowcaseDataService.Tests;

/// <summary>
/// Contract gate: SP name/signature expectations must stay aligned with the SQL project stub
/// and <see cref="ShowcaseDatabaseOptions"/> (single .NET config place for schema).
/// </summary>
public class SpContractTests
{
    [Fact]
    public void DefaultSchema_ProcedureName_MatchesSqlProject()
    {
        var database = new ShowcaseDatabaseOptions { Schema = ShowcaseDatabaseOptions.DefaultSchema };
        Assert.Equal("showcase.GetShowcaseSummary", database.GetShowcaseSummaryProcedure);

        var sqlPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "ShowcaseDataService.Database", "Programmability", "GetShowcaseSummary.sql"));
        Assert.True(File.Exists(sqlPath), $"Missing SQL stub at {sqlPath}");
        var sql = File.ReadAllText(sqlPath);
        Assert.Contains("[showcase].[GetShowcaseSummary]", sql);
        Assert.Contains("@Id UNIQUEIDENTIFIER", sql);
    }

    [Fact]
    public void Schema_And_Connection_Are_Configured_In_One_Place()
    {
        var database = new ShowcaseDatabaseOptions
        {
            Schema = "dbo",
            OwnedConnectionString = "Server=.;Database=ShowcaseOwned;Trusted_Connection=True;TrustServerCertificate=True"
        };

        Assert.Equal("dbo.GetShowcaseSummary", database.GetShowcaseSummaryProcedure);
        Assert.Equal("[dbo].[Items]", database.ItemsTable);
        Assert.Contains("Database=ShowcaseOwned", database.OwnedConnectionString);
        Assert.Equal(ShowcaseDatabaseOptions.SectionName, "Database");
    }
}
