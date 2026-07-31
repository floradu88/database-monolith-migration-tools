IF DATABASE_PRINCIPAL_ID(N'ds_runtime') IS NULL CREATE ROLE ds_runtime;
IF DATABASE_PRINCIPAL_ID(N'ds_readonly') IS NULL CREATE ROLE ds_readonly;
IF DATABASE_PRINCIPAL_ID(N'ds_migration') IS NULL CREATE ROLE ds_migration;
IF DATABASE_PRINCIPAL_ID(N'ds_operations') IS NULL CREATE ROLE ds_operations;
IF DATABASE_PRINCIPAL_ID(N'ds_audit_reader') IS NULL CREATE ROLE ds_audit_reader;
GO

-- Example:
-- GRANT EXECUTE ON SCHEMA::[app] TO ds_runtime;
-- GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[app] TO ds_runtime;
-- GRANT SELECT ON SCHEMA::[app] TO ds_readonly;
-- GRANT CONTROL ON SCHEMA::[app] TO ds_migration;
-- GRANT VIEW DEFINITION TO ds_operations;
-- Avoid db_owner for runtime identities.
