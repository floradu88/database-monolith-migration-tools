/*
Collector contract for hourly usage aggregation.
Raw Query Store, Audit, XE, and DMV events should be normalized by the
DbIntelligence worker and upserted into telemetry.DatabaseObjectUsageHourly.
This stored procedure accepts already-aggregated values and does not capture
row contents or SQL parameter values.
*/
CREATE OR ALTER PROCEDURE telemetry.UpsertDatabaseObjectUsageHourly
    @UsageDateHour datetime2(0),
    @DatabaseObjectId bigint,
    @ApplicationId bigint = NULL,
    @ActionName varchar(30),
    @ExecutionCount bigint,
    @FailureCount bigint,
    @TotalDurationMs decimal(38,3) = NULL,
    @MaximumDurationMs decimal(19,3) = NULL,
    @TotalCpuMs decimal(38,3) = NULL,
    @TotalLogicalReads bigint = NULL,
    @TotalLogicalWrites bigint = NULL,
    @FirstSeenUtc datetime2(3),
    @LastSeenUtc datetime2(3),
    @AttributionConfidence decimal(5,4) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE telemetry.DatabaseObjectUsageHourly
       SET ExecutionCount = ExecutionCount + @ExecutionCount,
           FailureCount = FailureCount + @FailureCount,
           TotalDurationMs = COALESCE(TotalDurationMs,0) + COALESCE(@TotalDurationMs,0),
           MaximumDurationMs = CASE
               WHEN MaximumDurationMs IS NULL THEN @MaximumDurationMs
               WHEN @MaximumDurationMs IS NULL THEN MaximumDurationMs
               WHEN @MaximumDurationMs > MaximumDurationMs THEN @MaximumDurationMs
               ELSE MaximumDurationMs END,
           TotalCpuMs = COALESCE(TotalCpuMs,0) + COALESCE(@TotalCpuMs,0),
           TotalLogicalReads = COALESCE(TotalLogicalReads,0) + COALESCE(@TotalLogicalReads,0),
           TotalLogicalWrites = COALESCE(TotalLogicalWrites,0) + COALESCE(@TotalLogicalWrites,0),
           FirstSeenUtc = CASE WHEN @FirstSeenUtc < FirstSeenUtc THEN @FirstSeenUtc ELSE FirstSeenUtc END,
           LastSeenUtc = CASE WHEN @LastSeenUtc > LastSeenUtc THEN @LastSeenUtc ELSE LastSeenUtc END,
           AttributionConfidence = COALESCE(@AttributionConfidence, AttributionConfidence)
     WHERE UsageDateHour = @UsageDateHour
       AND DatabaseObjectId = @DatabaseObjectId
       AND ((ApplicationId = @ApplicationId) OR (ApplicationId IS NULL AND @ApplicationId IS NULL))
       AND ActionName = @ActionName;

    IF @@ROWCOUNT = 0
    BEGIN
        INSERT telemetry.DatabaseObjectUsageHourly
        (
            UsageDateHour, DatabaseObjectId, ApplicationId, ActionName,
            ExecutionCount, FailureCount, TotalDurationMs, MaximumDurationMs,
            TotalCpuMs, TotalLogicalReads, TotalLogicalWrites,
            FirstSeenUtc, LastSeenUtc, AttributionConfidence
        )
        VALUES
        (
            @UsageDateHour, @DatabaseObjectId, @ApplicationId, @ActionName,
            @ExecutionCount, @FailureCount, @TotalDurationMs, @MaximumDurationMs,
            @TotalCpuMs, @TotalLogicalReads, @TotalLogicalWrites,
            @FirstSeenUtc, @LastSeenUtc, @AttributionConfidence
        );
    END;
END;
