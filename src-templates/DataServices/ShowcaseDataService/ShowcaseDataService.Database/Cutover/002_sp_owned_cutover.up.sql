-- Cutover 002 UP — Owned cutover after dacpac published showcase.GetShowcaseSummary.
-- Intent: mark owned path ready; revoke façade-only permissions when approved.
-- Prefer permission revoke over replacing SP body with RAISERROR.

PRINT 'Cutover 002 UP: owned SP cutover wave (stub) — requires owner + DBA approval';
GO

-- Example (do not invent principals):
-- REVOKE EXECUTE ON OBJECT::[showcase].[GetShowcaseSummary_Source] FROM [app_rw];
-- GRANT EXECUTE ON OBJECT::[showcase].[GetShowcaseSummary] TO [app_rw];
GO
