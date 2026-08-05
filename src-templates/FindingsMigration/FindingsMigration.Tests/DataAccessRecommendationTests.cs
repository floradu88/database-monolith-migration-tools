using FindingsMigration.Contracts;
using FindingsMigration.Core;
using Xunit;

namespace FindingsMigration.Tests;

public class DataAccessRecommendationTests
{
    [Fact]
    public void Recommend_ef_for_simple_crud_table()
    {
        var hint = DataAccessRecommendation.Recommend(new CodeToDbEntry
        {
            DbKind = "Table",
            Relation = "READS",
            Pattern = "ef-linq"
        });
        Assert.Contains("EF Core", hint);
    }

    [Fact]
    public void Recommend_dapper_or_sp_for_procedure()
    {
        var hint = DataAccessRecommendation.Recommend(new CodeToDbEntry
        {
            DbKind = "StoredProcedure",
            Relation = "EXECUTES",
            Pattern = "dapper-procedure"
        });
        Assert.Contains("Stored procedure", hint);
    }
}
