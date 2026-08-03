using BuildingBlocks.DataAccess.Abstractions;
using ShowcaseDataService.Domain;
using ShowcaseDataService.Infrastructure.StoredProcedures;
using Xunit;

namespace ShowcaseDataService.Tests;

public class StoredProcedureNameTests
{
    [Fact]
    public void Format_Uses_Enum_Type_Names_As_Holes()
    {
        var name = StoredProcedureName.Format(
            ShowcaseProcedureNames.ReportTemplate,
            ShowcaseReportArea.Sales,
            ShowcaseReportAction.Summary);

        Assert.Equal("usp_Showcase_Sales_Summary", name);
    }

    [Fact]
    public void Expand_Cartesian_From_Enums()
    {
        var all = ShowcaseProcedureNames.ExpandReports();
        Assert.Contains("usp_Showcase_Sales_Summary", all);
        Assert.Contains("usp_Showcase_Inventory_Detail", all);
        Assert.Equal(4, all.Count);
    }

    [Fact]
    public void TryParseInterpolatedTemplate_From_Dollar_String()
    {
        var template = StoredProcedureName.TryParseInterpolatedTemplate("$\"usp_{area}_{action}\"");
        Assert.Equal("usp_{area}_{action}", template);
    }

    [Fact]
    public void Resolve_With_Dictionary_Tokens()
    {
        var resolved = StoredProcedureName.Resolve(
            "usp_{ValueA}_{ValueB}",
            new Dictionary<string, string>
            {
                ["ValueA"] = "Alpha",
                ["ValueB"] = "Beta"
            });
        Assert.Equal("usp_Alpha_Beta", resolved);
    }
}
