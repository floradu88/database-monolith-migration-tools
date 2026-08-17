-- Cutover 003 DOWN — disable pair; retain core tables and evidence for investigation.

PRINT 'Cutover 003 DOWN: disable showcase-workitem dual-write pair (no DROP)';
GO

IF OBJECT_ID(N'[core].[DualWritePair]', N'U') IS NOT NULL
    UPDATE [core].[DualWritePair]
       SET [Enabled] = 0,
           [Notes] = N'Disabled by 003 down. Retain core for investigation.'
     WHERE [PairName] = N'showcase-workitem';
GO
