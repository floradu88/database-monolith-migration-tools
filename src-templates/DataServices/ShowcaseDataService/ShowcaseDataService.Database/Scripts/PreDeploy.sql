-- Pre-deploy: foundation guards only. DBA review required before production apply.
-- Never auto-execute destructive changes.
-- Deploy order: PreDeploy → EF migrations → dacpac → Cutover/*.up.sql → PostDeploy.

PRINT 'ShowcaseDataService PreDeploy — schema/guards';
GO

-- Ensure schemas exist before EF or dacpac (idempotent).
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'showcase')
    EXEC(N'CREATE SCHEMA [showcase]');
GO
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'deployment')
    EXEC(N'CREATE SCHEMA [deployment]');
GO
