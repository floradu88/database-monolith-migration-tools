/*
RBAC stubs for dbo → core parallel-write quality window.
Align with 21-create-rbac-roles.sql. Do not invent principals. DBA review required.
dbo remains the caller-facing writer; core is the candidate schema.
*/
GO

-- CREATE ROLE ds_runtime / ds_readonly / ds_migration / ds_operations first (21-create-rbac-roles.sql).

-- Runtime: execute dbo (source of truth) + core candidate SPs; DML on both write tables.
-- GRANT EXECUTE ON SCHEMA::[dbo] TO ds_runtime;
-- GRANT EXECUTE ON SCHEMA::[core] TO ds_runtime;
-- GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[core] TO ds_runtime;

-- Integrity / metrics: operators and migration identities, not app runtime if separated.
-- GRANT EXECUTE ON OBJECT::core.usp_TableIntegrity_Check TO ds_operations;
-- GRANT EXECUTE ON OBJECT::core.usp_RegisterDualWritePair TO ds_migration;
-- GRANT EXECUTE ON OBJECT::core.usp_LogDualWriteCall TO ds_runtime;
-- GRANT EXECUTE ON OBJECT::core.usp_RollupDualWriteMetricsHourly TO ds_operations;
-- GRANT SELECT ON OBJECT::core.DualWriteEvidence TO ds_operations;
-- GRANT SELECT ON OBJECT::core.DualWriteMetricsHourly TO ds_operations;
-- GRANT SELECT ON OBJECT::core.DualWriteCallLog TO ds_audit_reader;

-- Avoid db_owner for runtime identities.
-- Revoke core DML from app runtime after cutover if core becomes the only writer and dbo is façade-only.
SELECT N'core dual-write RBAC is commented by design — bind to approved principals.' AS Instruction;
GO
