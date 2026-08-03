using BuildingBlocks.DataAccess.Abstractions;
using ShowcaseDataService.Domain;

namespace ShowcaseDataService.Infrastructure.StoredProcedures;

/// <summary>
/// Single place for Showcase procedure name templates. Holes map to enums/constants
/// so <c>$"{area}_{action}"</c>-style call sites stay discoverable and resolvable.
/// </summary>
public static class ShowcaseProcedureNames
{
    /// <summary>Template form used by FindingsMigration / DbIntelligence expansion.</summary>
    public const string ReportTemplate = "usp_Showcase_{ShowcaseReportArea}_{ShowcaseReportAction}";

    public static string Report(ShowcaseReportArea area, ShowcaseReportAction action) =>
        StoredProcedureName.Format(ReportTemplate, area, action);

    public static IReadOnlyList<string> ExpandReports() =>
        StoredProcedureName.Expand(
            ReportTemplate,
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                [nameof(ShowcaseReportArea)] = Enum.GetNames<ShowcaseReportArea>(),
                [nameof(ShowcaseReportAction)] = Enum.GetNames<ShowcaseReportAction>()
            });
}
