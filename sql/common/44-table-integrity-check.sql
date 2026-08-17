/*
SP-write subset check for one DualWritePair. Evidence only — never fails the caller.
core must be a subset of dbo on CompareColumns (every core SP-written row exists in dbo).
Extra dbo rows (history, EF, ad-hoc SQL, jobs, other SPs) are expected and do not fail the check.
Requires 40 + 42. DBA review before production. Uses QUOTENAME / sys.columns only.
*/
SET XACT_ABORT ON;
GO

CREATE OR ALTER PROCEDURE core.usp_TableIntegrity_Check
    @PairId int = NULL,
    @PairName nvarchar(128) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @started datetime2(3) = SYSUTCDATETIME();
    DECLARE @id int;
    DECLARE @srcSchema sysname, @srcTable sysname, @tgtSchema sysname, @tgtTable sysname;
    DECLARE @compare nvarchar(1000), @watermark sysname, @maxId bigint, @t0 datetime2(3), @enabled bit;

    SELECT TOP (1)
        @id = PairId,
        @srcSchema = SourceSchema,
        @srcTable = SourceTable,
        @tgtSchema = TargetSchema,
        @tgtTable = TargetTable,
        @compare = CompareColumns,
        @watermark = WatermarkColumn,
        @maxId = DboMaxIdAtStart,
        @t0 = StartedAtUtc,
        @enabled = Enabled
    FROM core.DualWritePair
    WHERE (@PairId IS NOT NULL AND PairId = @PairId)
       OR (@PairId IS NULL AND @PairName IS NOT NULL AND PairName = @PairName)
       OR (@PairId IS NULL AND @PairName IS NULL AND Enabled = 1)
    ORDER BY PairId;

    IF @id IS NULL
    BEGIN
        RAISERROR(N'No DualWritePair matched the request.', 16, 1);
        RETURN;
    END;

    IF @enabled = 0
    BEGIN
        SELECT @id AS PairId, CAST(1 AS bit) AS IsMatch, 0 AS DboDeltaCount, 0 AS CoreCount,
               0 AS MissingInCoreCount, 0 AS MissingInDboCount, 0 AS DurationMs,
               N'Pair disabled.' AS SampleDiff;
        RETURN;
    END;

    DECLARE @src nvarchar(512) = QUOTENAME(@srcSchema) + N'.' + QUOTENAME(@srcTable);
    DECLARE @tgt nvarchar(512) = QUOTENAME(@tgtSchema) + N'.' + QUOTENAME(@tgtTable);

    IF OBJECT_ID(@src, N'U') IS NULL OR OBJECT_ID(@tgt, N'U') IS NULL
    BEGIN
        RAISERROR(N'Source or target table missing for pair %d.', 16, 1, @id);
        RETURN;
    END;

    DECLARE @col sysname;
    DECLARE @selectList nvarchar(max) = N'';
    DECLARE @pos int = 1, @next int, @token nvarchar(128);

    WHILE @pos <= LEN(@compare)
    BEGIN
        SET @next = CHARINDEX(N',', @compare, @pos);
        IF @next = 0 SET @next = LEN(@compare) + 1;
        SET @token = LTRIM(RTRIM(SUBSTRING(@compare, @pos, @next - @pos)));
        IF LEN(@token) > 0
        BEGIN
            IF COL_LENGTH(@src, @token) IS NULL OR COL_LENGTH(@tgt, @token) IS NULL
            BEGIN
                RAISERROR(N'Compare column [%s] is not on both tables.', 16, 1, @token);
                RETURN;
            END;
            SET @selectList = @selectList + CASE WHEN @selectList = N'' THEN N'' ELSE N', ' END + QUOTENAME(@token);
        END;
        SET @pos = @next + 1;
    END;

    IF @selectList = N''
    BEGIN
        RAISERROR(N'CompareColumns is empty for pair %d.', 16, 1, @id);
        RETURN;
    END;

    -- Compare core against the full dbo table so extra dbo writers are not treated as mismatches.
    DECLARE @sql nvarchar(max) = N'
        DECLARE @missingCore int, @missingDbo int, @dboCnt int, @coreCnt int;

        SELECT @dboCnt = COUNT(*) FROM ' + @src + N';
        SELECT @coreCnt = COUNT(*) FROM ' + @tgt + N';

        SELECT @missingCore = COUNT(*) FROM (
            SELECT ' + @selectList + N' FROM ' + @src + N'
            EXCEPT
            SELECT ' + @selectList + N' FROM ' + @tgt + N'
        ) x;

        SELECT @missingDbo = COUNT(*) FROM (
            SELECT ' + @selectList + N' FROM ' + @tgt + N'
            EXCEPT
            SELECT ' + @selectList + N' FROM ' + @src + N'
        ) x;

        SELECT @missingCore AS MissingInCoreCount, @missingDbo AS MissingInDboCount,
               @dboCnt AS DboDeltaCount, @coreCnt AS CoreCount;
    ';

    DECLARE @missingCore int, @missingDbo int, @dboCnt int, @coreCnt int;
    DECLARE @counts TABLE (
        MissingInCoreCount int,
        MissingInDboCount int,
        DboDeltaCount int,
        CoreCount int
    );

    INSERT @counts
    EXEC sys.sp_executesql @sql;

    SELECT
        @missingCore = MissingInCoreCount,
        @missingDbo = MissingInDboCount,
        @dboCnt = DboDeltaCount,
        @coreCnt = CoreCount
    FROM @counts;

    DECLARE @isMatch bit = CASE WHEN @missingDbo = 0 THEN 1 ELSE 0 END;
    DECLARE @ms int = DATEDIFF(millisecond, @started, SYSUTCDATETIME());
    DECLARE @sample nvarchar(400) = CASE WHEN @isMatch = 1 THEN
            CASE WHEN @missingCore > 0 THEN N'extraDboRows=' + CONVERT(nvarchar(20), @missingCore) + N' (expected; not a fail)' ELSE NULL END
        ELSE N'coreRowsNotInDbo=' + CONVERT(nvarchar(20), @missingDbo)
           + N'; extraDboRows=' + CONVERT(nvarchar(20), @missingCore)
        END;

    INSERT core.DualWriteEvidence (
        PairId, IsMatch, DboDeltaCount, CoreCount, MissingInCoreCount, MissingInDboCount, DurationMs, SampleDiff)
    VALUES (
        @id, @isMatch, @dboCnt, @coreCnt, @missingCore, @missingDbo, @ms, @sample);

    SELECT
        @id AS PairId,
        @isMatch AS IsMatch,
        @dboCnt AS DboDeltaCount,
        @coreCnt AS CoreCount,
        @missingCore AS MissingInCoreCount,
        @missingDbo AS MissingInDboCount,
        @ms AS DurationMs,
        @sample AS SampleDiff,
        SYSUTCDATETIME() AS CheckedAtUtc;
END;
GO
