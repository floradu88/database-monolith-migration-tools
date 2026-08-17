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
GO

CREATE TABLE [core].[DualWriteCallLog]
(
    [CallLogId] bigint IDENTITY(1,1) NOT NULL CONSTRAINT [PK_core_DualWriteCallLog] PRIMARY KEY,
    [PairId] int NULL CONSTRAINT [FK_core_DualWriteCallLog_Pair] FOREIGN KEY REFERENCES [core].[DualWritePair] ([PairId]),
    [Operation] nvarchar(128) NOT NULL,
    [BusinessKey] nvarchar(200) NOT NULL,
    [CorrelationId] uniqueidentifier NOT NULL CONSTRAINT [DF_core_DualWriteCallLog_Corr] DEFAULT NEWSEQUENTIALID(),
    [DboSucceeded] bit NOT NULL,
    [CoreSucceeded] bit NOT NULL,
    [CoreTimedOut] bit NOT NULL CONSTRAINT [DF_core_DualWriteCallLog_Timeout] DEFAULT 0,
    [DboDurationMs] int NULL,
    [CoreDurationMs] int NULL,
    [CoreError] nvarchar(400) NULL,
    [CalledAtUtc] datetime2(3) NOT NULL CONSTRAINT [DF_core_DualWriteCallLog_Called] DEFAULT SYSUTCDATETIME()
);
GO

CREATE INDEX [IX_core_DualWriteCallLog_Pair_Called] ON [core].[DualWriteCallLog] ([PairId], [CalledAtUtc]);
GO

CREATE TABLE [core].[DualWriteEvidence]
(
    [EvidenceId] bigint IDENTITY(1,1) NOT NULL CONSTRAINT [PK_core_DualWriteEvidence] PRIMARY KEY,
    [PairId] int NOT NULL CONSTRAINT [FK_core_DualWriteEvidence_Pair] FOREIGN KEY REFERENCES [core].[DualWritePair] ([PairId]),
    [CheckedAtUtc] datetime2(3) NOT NULL CONSTRAINT [DF_core_DualWriteEvidence_Checked] DEFAULT SYSUTCDATETIME(),
    [IsMatch] bit NOT NULL,
    -- MissingInCoreCount = extra dbo rows (expected). MissingInDboCount = core SP rows not in dbo (mismatch).
    [DboDeltaCount] int NOT NULL,
    [CoreCount] int NOT NULL,
    [MissingInCoreCount] int NOT NULL,
    [MissingInDboCount] int NOT NULL,
    [DurationMs] int NOT NULL,
    [SampleDiff] nvarchar(max) NULL
);
GO
