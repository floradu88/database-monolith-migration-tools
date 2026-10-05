-- Ownership: SqlProject

CREATE INDEX [IX_core_DualWriteCallLog_Pair_Called]
    ON [core].[DualWriteCallLog] ([PairId], [CalledAtUtc]);
