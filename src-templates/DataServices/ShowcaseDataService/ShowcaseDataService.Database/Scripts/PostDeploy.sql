-- Post-deploy: permissions / grants placeholders. Align with sql/common/21-create-rbac-roles.sql (DBA-review).
PRINT 'ShowcaseDataService PostDeploy — RBAC grants stub (no db_owner at runtime)';
-- Example (do not invent principals):
-- GRANT SELECT, EXECUTE ON SCHEMA::showcase TO [app_rw];
GO
