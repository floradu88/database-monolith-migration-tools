namespace ShowcaseDataService.Domain;

/// <summary>
/// Token for templated procedure names like <c>usp_Showcase_{ShowcaseReportArea}_{ShowcaseReportAction}</c>.
/// Prefer enums over free-form strings so scanners can expand and find concrete SPs.
/// </summary>
public enum ShowcaseReportArea
{
    Sales = 0,
    Inventory = 1
}

/// <summary>Second segment for templated Showcase report procedures.</summary>
public enum ShowcaseReportAction
{
    Summary = 0,
    Detail = 1
}
