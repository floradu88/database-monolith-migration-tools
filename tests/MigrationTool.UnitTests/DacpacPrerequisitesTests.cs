using MigrationTool.Infrastructure;

namespace MigrationTool.UnitTests;

public sealed class DacpacPrerequisitesTests
{
    [Fact]
    public void Reports_dotnet_or_explains_gap()
    {
        var report = ExternalToolLocator.GetPrerequisites();

        Assert.NotEmpty(report.Tools);
        Assert.Contains(report.Tools, t => t.Name.Contains("dotnet", StringComparison.OrdinalIgnoreCase));
        Assert.False(string.IsNullOrWhiteSpace(report.Format()));
    }
}
