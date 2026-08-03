-- Cutover 001 UP — FacadeThenMove compatibility stub (DBA review before apply).
-- Intent: document façade-era wrapper placement; SourceFacade still calls monolith until Owned.
-- Does not drop or rewrite production objects.

PRINT 'Cutover 001 UP: showcase SP façade wave (stub) — no destructive changes';
GO

-- Example (do not invent principals or linked servers):
-- CREATE OR ALTER SYNONYM [showcase].[GetShowcaseSummary_Source]
--   FOR [MonolithDb].[dbo].[GetShowcaseSummary];
GO
