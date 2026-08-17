-- Cutover 003 UP — register dbo/core ShowcaseWorkItem pair (delta-only, no backfill).
-- DBA review before apply. Same database; dbo remains caller source of truth.

PRINT 'Cutover 003 UP: register showcase-workitem dual-write pair';
GO

IF OBJECT_ID(N'[core].[usp_RegisterDualWritePair]', N'P') IS NOT NULL
BEGIN
    EXEC [core].[usp_RegisterDualWritePair]
        @PairName = N'showcase-workitem',
        @SourceSchema = N'dbo',
        @SourceTable = N'ShowcaseWorkItem',
        @TargetSchema = N'core',
        @TargetTable = N'ShowcaseWorkItem',
        @SourceProcedure = N'usp_ShowcaseWorkItem_Upsert',
        @TargetProcedure = N'usp_ShowcaseWorkItem_Upsert',
        @BusinessKeyColumns = N'ExternalId',
        @CompareColumns = N'ExternalId,Name,Status',
        @WatermarkColumn = N'UpdatedAt';
END
GO
