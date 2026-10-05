-- Ownership: SqlProject — dual-write quality-window control tables (same shape as sql/common/42).

CREATE TABLE [core].[DualWritePair]
(
    [PairId] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_core_DualWritePair] PRIMARY KEY,
    [PairName] nvarchar(128) NOT NULL,
    [SourceSchema] sysname NOT NULL CONSTRAINT [DF_core_DualWritePair_SourceSchema] DEFAULT N'dbo',
    [SourceTable] sysname NOT NULL,
    [TargetSchema] sysname NOT NULL CONSTRAINT [DF_core_DualWritePair_TargetSchema] DEFAULT N'core',
    [TargetTable] sysname NOT NULL,
    [SourceProcedure] sysname NULL,
    [TargetProcedure] sysname NULL,
    [BusinessKeyColumns] nvarchar(500) NOT NULL,
    [CompareColumns] nvarchar(1000) NOT NULL,
    [WatermarkColumn] sysname NULL,
    [DboMaxIdAtStart] bigint NULL,
    [StartedAtUtc] datetime2(3) NOT NULL CONSTRAINT [DF_core_DualWritePair_Started] DEFAULT SYSUTCDATETIME(),
    [Enabled] bit NOT NULL CONSTRAINT [DF_core_DualWritePair_Enabled] DEFAULT 1,
    [Notes] nvarchar(400) NULL,
    CONSTRAINT [UQ_core_DualWritePair] UNIQUE ([SourceSchema], [SourceTable], [TargetSchema], [TargetTable])
);
