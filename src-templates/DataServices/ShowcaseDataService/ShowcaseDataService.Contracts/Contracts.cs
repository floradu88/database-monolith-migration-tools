namespace ShowcaseDataService.Contracts;

public sealed record ShowcaseItemDto(Guid Id, string Name, string Status, DateTimeOffset UpdatedAt);

public sealed record ShowcaseSummaryDto(Guid Id, string Name, string Status, string Source);

public sealed record ShowcaseUpdateRequest(Guid Id, string Name, string Status);

public sealed record AccessMethodStatDto(
    string Operation,
    string Method,
    int Samples,
    double AvgMs,
    long MinMs,
    long MaxMs,
    long P95Ms,
    bool IsFastest);

public sealed record SloCountersDto(
    long Requests,
    long Errors,
    double ErrorRatePercent,
    long P95Ms,
    double LatencyBudgetMs,
    bool WithinLatencyBudget,
    bool WithinErrorBudget);

public sealed record ShowcaseDashboardDto(
    string Slot,
    string DefaultRoute,
    string AuthoritativeMethod,
    int ShadowComparisons,
    int MatchingShadows,
    int MismatchingShadows,
    IReadOnlyList<ShadowDiffDto> RecentDiffs,
    IReadOnlyList<AccessMethodStatDto> AccessMethodStats,
    string? FastestMethodHint,
    SloCountersDto Slo,
    ParallelWriteDashboardDto ParallelWrite);

public sealed record ParallelWriteDashboardDto(
    int Calls,
    int DboFailures,
    int CoreFailures,
    int CoreTimeouts,
    int IntegrityChecks,
    int IntegrityMismatches,
    long DboP95Ms,
    long CoreP95Ms,
    bool LastIntegrityMatch,
    IReadOnlyList<ParallelWriteCallDto> RecentCalls,
    IReadOnlyList<TableIntegrityDto> RecentIntegrity);

public sealed record ParallelWriteCallDto(
    string Operation,
    string BusinessKey,
    bool DboSucceeded,
    bool CoreSucceeded,
    bool CoreTimedOut,
    long DboDurationMs,
    long CoreDurationMs,
    string? CoreError,
    DateTimeOffset CalledAt);

public sealed record TableIntegrityDto(
    string PairName,
    bool IsMatch,
    int DboDeltaCount,
    int CoreCount,
    int MissingInCoreCount,
    int MissingInDboCount,
    long DurationMs,
    string? SampleDiff,
    DateTimeOffset CheckedAt);

public sealed record ShowcaseWorkItemRequest(Guid ExternalId, string Name, string Status);

public sealed record TableIntegritySqlRow(
    int? PairId,
    bool IsMatch,
    int DboDeltaCount,
    int CoreCount,
    int MissingInCoreCount,
    int MissingInDboCount,
    int DurationMs,
    string? SampleDiff,
    DateTimeOffset CheckedAtUtc);

public sealed record ShadowDiffDto(
    string Operation,
    string RouteA,
    string RouteB,
    long ElapsedMsA,
    long ElapsedMsB,
    bool PayloadsMatch,
    string? DiffSummary,
    DateTimeOffset ComparedAt);

public sealed record AccessBenchmarkDto(
    Guid Id,
    ShowcaseSummaryDto? Ef,
    ShowcaseSummaryDto? StoredProcedure,
    ShowcaseSummaryDto? PlainSql,
    long EfMs,
    long SpMs,
    long SqlMs,
    string FastestMethod,
    bool PayloadsMatch,
    IReadOnlyList<AccessMethodStatDto> CumulativeStats);
