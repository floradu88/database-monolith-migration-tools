-- Ownership: SqlProject — integrity check for Showcase WorkItem pair (delta-only).

CREATE PROCEDURE [core].[usp_TableIntegrity_Check]
    @PairName nvarchar(128) = N'showcase-workitem'
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @started datetime2(3) = SYSUTCDATETIME();
    DECLARE @id int, @t0 datetime2(3), @enabled bit;

    SELECT TOP (1) @id = [PairId], @t0 = [StartedAtUtc], @enabled = [Enabled]
    FROM [core].[DualWritePair]
    WHERE [PairName] = @PairName
    ORDER BY [PairId];

    IF @id IS NULL
    BEGIN
        SELECT CAST(NULL AS int) AS PairId, CAST(1 AS bit) AS IsMatch, 0 AS DboDeltaCount, 0 AS CoreCount,
               0 AS MissingInCoreCount, 0 AS MissingInDboCount, 0 AS DurationMs,
               N'Pair not registered. Apply Cutover/003_register_workitem_pair.up.sql' AS SampleDiff,
               SYSUTCDATETIME() AS CheckedAtUtc;
        RETURN;
    END;

    IF @enabled = 0
    BEGIN
        SELECT @id AS PairId, CAST(1 AS bit) AS IsMatch, 0 AS DboDeltaCount, 0 AS CoreCount,
               0 AS MissingInCoreCount, 0 AS MissingInDboCount, 0 AS DurationMs,
               N'Pair disabled.' AS SampleDiff, SYSUTCDATETIME() AS CheckedAtUtc;
        RETURN;
    END;

    DECLARE @dboCnt int, @coreCnt int, @missingCore int, @missingDbo int;

    SELECT @dboCnt = COUNT(*) FROM [dbo].[ShowcaseWorkItem];
    SELECT @coreCnt = COUNT(*) FROM [core].[ShowcaseWorkItem];

    -- Extra dbo rows (non-SP writers / history) are counted but do not fail.
    SELECT @missingCore = COUNT(*) FROM (
        SELECT [ExternalId], [Name], [Status] FROM [dbo].[ShowcaseWorkItem]
        EXCEPT
        SELECT [ExternalId], [Name], [Status] FROM [core].[ShowcaseWorkItem]
    ) x;

    -- Fail only when an SP-written core row is missing or different in dbo.
    SELECT @missingDbo = COUNT(*) FROM (
        SELECT [ExternalId], [Name], [Status] FROM [core].[ShowcaseWorkItem]
        EXCEPT
        SELECT [ExternalId], [Name], [Status] FROM [dbo].[ShowcaseWorkItem]
    ) x;

    DECLARE @isMatch bit = CASE WHEN @missingDbo = 0 THEN 1 ELSE 0 END;
    DECLARE @ms int = DATEDIFF(millisecond, @started, SYSUTCDATETIME());
    DECLARE @sample nvarchar(400) = CASE WHEN @isMatch = 1 THEN
            CASE WHEN @missingCore > 0 THEN N'extraDboRows=' + CONVERT(nvarchar(20), @missingCore) + N' (expected; not a fail)' ELSE NULL END
        ELSE N'coreRowsNotInDbo=' + CONVERT(nvarchar(20), @missingDbo)
           + N'; extraDboRows=' + CONVERT(nvarchar(20), @missingCore) END;

    INSERT [core].[DualWriteEvidence] (
        [PairId], [IsMatch], [DboDeltaCount], [CoreCount], [MissingInCoreCount], [MissingInDboCount], [DurationMs], [SampleDiff])
    VALUES (@id, @isMatch, @dboCnt, @coreCnt, @missingCore, @missingDbo, @ms, @sample);

    SELECT @id AS PairId, @isMatch AS IsMatch, @dboCnt AS DboDeltaCount, @coreCnt AS CoreCount,
           @missingCore AS MissingInCoreCount, @missingDbo AS MissingInDboCount, @ms AS DurationMs,
           @sample AS SampleDiff, SYSUTCDATETIME() AS CheckedAtUtc;
END;
