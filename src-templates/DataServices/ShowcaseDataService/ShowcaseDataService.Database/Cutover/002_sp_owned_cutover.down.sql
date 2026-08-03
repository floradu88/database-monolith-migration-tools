-- Cutover 002 DOWN — roll owned cutover back to façade-friendly permissions.

PRINT 'Cutover 002 DOWN: restore façade-friendly execute path (stub)';
GO

-- Example (do not invent principals):
-- REVOKE EXECUTE ON OBJECT::[showcase].[GetShowcaseSummary] FROM [app_rw];
-- GRANT EXECUTE ON OBJECT::[showcase].[GetShowcaseSummary_Source] TO [app_rw];
GO
