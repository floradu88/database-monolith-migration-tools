-- Cutover 001 DOWN — reverse façade wrapper wave (DBA review before apply).

PRINT 'Cutover 001 DOWN: remove showcase SP façade stubs (stub) — no destructive defaults';
GO

-- Example:
-- DROP SYNONYM IF EXISTS [showcase].[GetShowcaseSummary_Source];
GO
