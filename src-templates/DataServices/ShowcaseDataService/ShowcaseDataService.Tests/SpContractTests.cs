using ShowcaseDataService.Infrastructure.StoredProcedures;
using Xunit;

namespace ShowcaseDataService.Tests;

/// <summary>
/// Contract gate: SP name/signature expectations must stay aligned with the SQL project stub.
/// </summary>
public class SpContractTests
{
    [Fact]
    public void GetShowcaseSummary_ProcedureName_MatchesSqlProject()
    {
        Assert.Equal("showcase.GetShowcaseSummary", SpGetShowcaseSummary.ProcedureName);
        var sqlPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "ShowcaseDataService.Database", "Programmability", "GetShowcaseSummary.sql"));
        Assert.True(File.Exists(sqlPath), $"Missing SQL stub at {sqlPath}");
        var sql = File.ReadAllText(sqlPath);
        Assert.Contains("[showcase].[GetShowcaseSummary]", sql);
        Assert.Contains("@Id UNIQUEIDENTIFIER", sql);
    }
}
