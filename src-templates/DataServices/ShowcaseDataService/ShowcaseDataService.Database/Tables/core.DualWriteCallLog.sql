-- Ownership: SqlProject — dual-write call log.

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
