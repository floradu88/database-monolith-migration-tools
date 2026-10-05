-- Ownership: SqlProject — register dual-write pair metadata.

CREATE PROCEDURE [core].[usp_RegisterDualWritePair]
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
    MERGE [core].[DualWritePair] AS t
    USING (SELECT @SourceSchema AS SourceSchema, @SourceTable AS SourceTable, @TargetSchema AS TargetSchema, @TargetTable AS TargetTable) AS s
    ON t.[SourceSchema] = s.SourceSchema AND t.[SourceTable] = s.SourceTable
       AND t.[TargetSchema] = s.TargetSchema AND t.[TargetTable] = s.TargetTable
    WHEN MATCHED THEN
        UPDATE SET
            [PairName] = @PairName,
            [SourceProcedure] = @SourceProcedure,
            [TargetProcedure] = @TargetProcedure,
            [BusinessKeyColumns] = @BusinessKeyColumns,
            [CompareColumns] = @CompareColumns,
            [WatermarkColumn] = @WatermarkColumn,
            [Enabled] = 1
    WHEN NOT MATCHED THEN
        INSERT ([PairName], [SourceSchema], [SourceTable], [TargetSchema], [TargetTable],
                [SourceProcedure], [TargetProcedure], [BusinessKeyColumns], [CompareColumns],
                [WatermarkColumn], [StartedAtUtc], [Enabled], [Notes])
        VALUES (@PairName, @SourceSchema, @SourceTable, @TargetSchema, @TargetTable,
                @SourceProcedure, @TargetProcedure, @BusinessKeyColumns, @CompareColumns,
                @WatermarkColumn, SYSUTCDATETIME(), 1, N'SP-write only. No historical backfill. dbo extras expected.');
END;
