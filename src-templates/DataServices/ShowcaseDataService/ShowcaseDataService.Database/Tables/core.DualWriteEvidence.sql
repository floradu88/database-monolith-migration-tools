-- Ownership: SqlProject — dual-write integrity evidence.

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
