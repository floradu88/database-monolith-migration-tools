/*
Control plane objects for dbo → core parallel-write quality window.
Delta-only + SP-write coverage: register stamps T0; does NOT copy historical rows.
core receives only the paired stored-procedure writes. Extra dbo rows from other writers are expected.
Requires sql/common/40-create-core-schema.sql.
DBA review required. Additive only.
*/
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'core.DualWritePair', N'U') IS NULL
BEGIN
    CREATE TABLE core.DualWritePair
    (
        PairId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_core_DualWritePair PRIMARY KEY,
        PairName nvarchar(128) NOT NULL,
        SourceSchema sysname NOT NULL CONSTRAINT DF_core_DualWritePair_SourceSchema DEFAULT N'dbo',
        SourceTable sysname NOT NULL,
        TargetSchema sysname NOT NULL CONSTRAINT DF_core_DualWritePair_TargetSchema DEFAULT N'core',
        TargetTable sysname NOT NULL,
        SourceProcedure sysname NULL,
        TargetProcedure sysname NULL,
        BusinessKeyColumns nvarchar(500) NOT NULL,
        CompareColumns nvarchar(1000) NOT NULL,
        WatermarkColumn sysname NULL,
        DboMaxIdAtStart bigint NULL,
        StartedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_core_DualWritePair_Started DEFAULT SYSUTCDATETIME(),
        Enabled bit NOT NULL CONSTRAINT DF_core_DualWritePair_Enabled DEFAULT 1,
        Notes nvarchar(400) NULL,
        CONSTRAINT UQ_core_DualWritePair UNIQUE (SourceSchema, SourceTable, TargetSchema, TargetTable)
    );
END;
GO

IF OBJECT_ID(N'core.DualWriteCallLog', N'U') IS NULL
BEGIN
    CREATE TABLE core.DualWriteCallLog
    (
        CallLogId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_core_DualWriteCallLog PRIMARY KEY,
        PairId int NULL CONSTRAINT FK_core_DualWriteCallLog_Pair REFERENCES core.DualWritePair (PairId),
        Operation nvarchar(128) NOT NULL,
        BusinessKey nvarchar(200) NOT NULL,
        CorrelationId uniqueidentifier NOT NULL CONSTRAINT DF_core_DualWriteCallLog_Corr DEFAULT NEWSEQUENTIALID(),
        DboSucceeded bit NOT NULL,
        CoreSucceeded bit NOT NULL,
        CoreTimedOut bit NOT NULL CONSTRAINT DF_core_DualWriteCallLog_Timeout DEFAULT 0,
        DboDurationMs int NULL,
        CoreDurationMs int NULL,
        CoreError nvarchar(400) NULL,
        CalledAtUtc datetime2(3) NOT NULL CONSTRAINT DF_core_DualWriteCallLog_Called DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_core_DualWriteCallLog_Pair_Called ON core.DualWriteCallLog (PairId, CalledAtUtc);
    CREATE INDEX IX_core_DualWriteCallLog_BusinessKey ON core.DualWriteCallLog (BusinessKey, CalledAtUtc);
END;
GO

IF OBJECT_ID(N'core.DualWriteEvidence', N'U') IS NULL
BEGIN
    CREATE TABLE core.DualWriteEvidence
    (
        EvidenceId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_core_DualWriteEvidence PRIMARY KEY,
        PairId int NOT NULL CONSTRAINT FK_core_DualWriteEvidence_Pair REFERENCES core.DualWritePair (PairId),
        CheckedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_core_DualWriteEvidence_Checked DEFAULT SYSUTCDATETIME(),
        IsMatch bit NOT NULL, -- 1 iff no core SP-written row is missing/different in dbo
        DboDeltaCount int NOT NULL, -- dbo row count (may exceed core)
        CoreCount int NOT NULL,
        MissingInCoreCount int NOT NULL, -- extra dbo rows; informational, not a fail
        MissingInDboCount int NOT NULL, -- core rows not in dbo; this is the mismatch
        DurationMs int NOT NULL,
        SampleDiff nvarchar(max) NULL
    );
    CREATE INDEX IX_core_DualWriteEvidence_Pair_Checked ON core.DualWriteEvidence (PairId, CheckedAtUtc DESC);
END;
GO

IF OBJECT_ID(N'core.DualWriteMetricsHourly', N'U') IS NULL
BEGIN
    CREATE TABLE core.DualWriteMetricsHourly
    (
        UsageDateHour datetime2(0) NOT NULL,
        PairId int NOT NULL,
        CallCount bigint NOT NULL CONSTRAINT DF_core_DWMH_Calls DEFAULT 0,
        DboFailureCount bigint NOT NULL CONSTRAINT DF_core_DWMH_DboFail DEFAULT 0,
        CoreFailureCount bigint NOT NULL CONSTRAINT DF_core_DWMH_CoreFail DEFAULT 0,
        CoreTimeoutCount bigint NOT NULL CONSTRAINT DF_core_DWMH_Timeout DEFAULT 0,
        IntegrityCheckCount bigint NOT NULL CONSTRAINT DF_core_DWMH_Checks DEFAULT 0,
        IntegrityMismatchCount bigint NOT NULL CONSTRAINT DF_core_DWMH_Mismatch DEFAULT 0,
        TotalDboDurationMs decimal(38, 3) NULL,
        TotalCoreDurationMs decimal(38, 3) NULL,
        MaximumDboDurationMs decimal(19, 3) NULL,
        MaximumCoreDurationMs decimal(19, 3) NULL,
        CONSTRAINT PK_core_DualWriteMetricsHourly PRIMARY KEY (UsageDateHour, PairId)
    );
END;
GO

CREATE OR ALTER PROCEDURE core.usp_RegisterDualWritePair
    @PairName nvarchar(128),
    @SourceSchema sysname = N'dbo',
    @SourceTable sysname,
    @TargetSchema sysname = N'core',
    @TargetTable sysname,
    @SourceProcedure sysname = NULL,
    @TargetProcedure sysname = NULL,
    @BusinessKeyColumns nvarchar(500),
    @CompareColumns nvarchar(1000),
    @WatermarkColumn sysname = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @maxId bigint = NULL;
    DECLARE @src nvarchar(512) = QUOTENAME(@SourceSchema) + N'.' + QUOTENAME(@SourceTable);

    IF OBJECT_ID(@src, N'U') IS NULL
    BEGIN
        RAISERROR(N'Source table not found: %s', 16, 1, @src);
        RETURN;
    END;

    IF COL_LENGTH(@src, N'Id') IS NOT NULL AND @WatermarkColumn IS NULL
    BEGIN
        DECLARE @idSql nvarchar(max) = N'SELECT @m = MAX(CONVERT(bigint, [Id])) FROM ' + @src;
        EXEC sys.sp_executesql @idSql, N'@m bigint OUTPUT', @m = @maxId OUTPUT;
    END;

    MERGE core.DualWritePair AS t
    USING (SELECT @SourceSchema AS SourceSchema, @SourceTable AS SourceTable, @TargetSchema AS TargetSchema, @TargetTable AS TargetTable) AS s
    ON t.SourceSchema = s.SourceSchema AND t.SourceTable = s.SourceTable
       AND t.TargetSchema = s.TargetSchema AND t.TargetTable = s.TargetTable
    WHEN MATCHED THEN
        UPDATE SET
            PairName = @PairName,
            SourceProcedure = @SourceProcedure,
            TargetProcedure = @TargetProcedure,
            BusinessKeyColumns = @BusinessKeyColumns,
            CompareColumns = @CompareColumns,
            WatermarkColumn = @WatermarkColumn,
            Enabled = 1,
            Notes = N'Re-registered; T0 preserved. No historical backfill.'
    WHEN NOT MATCHED THEN
        INSERT (
            PairName, SourceSchema, SourceTable, TargetSchema, TargetTable,
            SourceProcedure, TargetProcedure, BusinessKeyColumns, CompareColumns,
            WatermarkColumn, DboMaxIdAtStart, StartedAtUtc, Enabled, Notes)
        VALUES (
            @PairName, @SourceSchema, @SourceTable, @TargetSchema, @TargetTable,
            @SourceProcedure, @TargetProcedure, @BusinessKeyColumns, @CompareColumns,
            @WatermarkColumn, @maxId, SYSUTCDATETIME(), 1,
            N'SP-write window. core starts empty. dbo extras from other writers are expected.');

    SELECT PairId, PairName, StartedAtUtc, DboMaxIdAtStart, Enabled
    FROM core.DualWritePair
    WHERE SourceSchema = @SourceSchema AND SourceTable = @SourceTable
      AND TargetSchema = @TargetSchema AND TargetTable = @TargetTable;
END;
GO

CREATE OR ALTER PROCEDURE core.usp_LogDualWriteCall
    @PairId int = NULL,
    @Operation nvarchar(128),
    @BusinessKey nvarchar(200),
    @CorrelationId uniqueidentifier = NULL,
    @DboSucceeded bit,
    @CoreSucceeded bit,
    @CoreTimedOut bit = 0,
    @DboDurationMs int = NULL,
    @CoreDurationMs int = NULL,
    @CoreError nvarchar(400) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT core.DualWriteCallLog (
        PairId, Operation, BusinessKey, CorrelationId,
        DboSucceeded, CoreSucceeded, CoreTimedOut,
        DboDurationMs, CoreDurationMs, CoreError)
    VALUES (
        @PairId, @Operation, @BusinessKey, COALESCE(@CorrelationId, NEWSEQUENTIALID()),
        @DboSucceeded, @CoreSucceeded, @CoreTimedOut,
        @DboDurationMs, @CoreDurationMs, @CoreError);
END;
GO

CREATE OR ALTER PROCEDURE core.usp_RollupDualWriteMetricsHourly
    @UsageDateHour datetime2(0) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @UsageDateHour = COALESCE(@UsageDateHour, DATEADD(hour, DATEDIFF(hour, 0, SYSUTCDATETIME()), 0));
    DECLARE @from datetime2(0) = @UsageDateHour;
    DECLARE @to datetime2(0) = DATEADD(hour, 1, @from);

    MERGE core.DualWriteMetricsHourly AS t
    USING (
        SELECT
            c.PairId,
            COUNT_BIG(*) AS CallCount,
            SUM(CASE WHEN c.DboSucceeded = 0 THEN 1 ELSE 0 END) AS DboFailureCount,
            SUM(CASE WHEN c.CoreSucceeded = 0 THEN 1 ELSE 0 END) AS CoreFailureCount,
            SUM(CASE WHEN c.CoreTimedOut = 1 THEN 1 ELSE 0 END) AS CoreTimeoutCount,
            SUM(CONVERT(decimal(38, 3), c.DboDurationMs)) AS TotalDboDurationMs,
            SUM(CONVERT(decimal(38, 3), c.CoreDurationMs)) AS TotalCoreDurationMs,
            MAX(CONVERT(decimal(19, 3), c.DboDurationMs)) AS MaximumDboDurationMs,
            MAX(CONVERT(decimal(19, 3), c.CoreDurationMs)) AS MaximumCoreDurationMs
        FROM core.DualWriteCallLog c
        WHERE c.CalledAtUtc >= @from AND c.CalledAtUtc < @to
          AND c.PairId IS NOT NULL
        GROUP BY c.PairId
    ) AS s ON t.UsageDateHour = @from AND t.PairId = s.PairId
    WHEN MATCHED THEN
        UPDATE SET
            CallCount = s.CallCount,
            DboFailureCount = s.DboFailureCount,
            CoreFailureCount = s.CoreFailureCount,
            CoreTimeoutCount = s.CoreTimeoutCount,
            TotalDboDurationMs = s.TotalDboDurationMs,
            TotalCoreDurationMs = s.TotalCoreDurationMs,
            MaximumDboDurationMs = s.MaximumDboDurationMs,
            MaximumCoreDurationMs = s.MaximumCoreDurationMs
    WHEN NOT MATCHED THEN
        INSERT (
            UsageDateHour, PairId, CallCount, DboFailureCount, CoreFailureCount, CoreTimeoutCount,
            TotalDboDurationMs, TotalCoreDurationMs, MaximumDboDurationMs, MaximumCoreDurationMs)
        VALUES (
            @from, s.PairId, s.CallCount, s.DboFailureCount, s.CoreFailureCount, s.CoreTimeoutCount,
            s.TotalDboDurationMs, s.TotalCoreDurationMs, s.MaximumDboDurationMs, s.MaximumCoreDurationMs);

    MERGE core.DualWriteMetricsHourly AS t
    USING (
        SELECT
            e.PairId,
            COUNT_BIG(*) AS IntegrityCheckCount,
            SUM(CASE WHEN e.IsMatch = 0 THEN 1 ELSE 0 END) AS IntegrityMismatchCount
        FROM core.DualWriteEvidence e
        WHERE e.CheckedAtUtc >= @from AND e.CheckedAtUtc < @to
        GROUP BY e.PairId
    ) AS s ON t.UsageDateHour = @from AND t.PairId = s.PairId
    WHEN MATCHED THEN
        UPDATE SET
            IntegrityCheckCount = s.IntegrityCheckCount,
            IntegrityMismatchCount = s.IntegrityMismatchCount
    WHEN NOT MATCHED THEN
        INSERT (UsageDateHour, PairId, IntegrityCheckCount, IntegrityMismatchCount)
        VALUES (@from, s.PairId, s.IntegrityCheckCount, s.IntegrityMismatchCount);
END;
GO
