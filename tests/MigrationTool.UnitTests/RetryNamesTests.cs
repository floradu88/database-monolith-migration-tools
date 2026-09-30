using MigrationTool.Domain;

namespace MigrationTool.UnitTests;

public class RetryNamesTests
{
    [Fact]
    public void NextAttempt_IncrementsFromExistingFailedCopies()
    {
        var names = new[] { "001_city.sql", "002_city.sql", "001_other.sql" };

        Assert.Equal(3, RetryNames.NextAttempt(names, "city.sql"));
        Assert.Equal("003_city.sql", RetryNames.FailureScriptName(3, "city.sql"));
        Assert.Equal("003_city.error.txt", RetryNames.FailureErrorName(3, "city.sql"));
    }
}
