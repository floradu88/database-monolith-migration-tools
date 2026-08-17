-- Post-deploy: contract stamp + RBAC placeholders.
-- Align grants with sql/common/21-create-rbac-roles.sql (DBA-review only).
-- Cutover up scripts are applied separately (see Cutover/README.md) — not inlined here.

PRINT 'ShowcaseDataService PostDeploy — contract stamp + RBAC stub (no db_owner at runtime)';
GO

IF OBJECT_ID(N'[deployment].[DatabaseContract]', N'U') IS NOT NULL
BEGIN
    MERGE [deployment].[DatabaseContract] AS t
    USING (VALUES
        (N'schema_version', N'1.0.0'),
        (N'contract_version', N'1.0.0-showcase'),
        (N'sql_project', N'ShowcaseDataService.Database'),
        (N'ef_migrations_project', N'ShowcaseDataService.Migrations'),
        (N'host_providers', N'OnPrem|Azure|Aws')
    ) AS s([ContractKey], [ContractValue])
    ON t.[ContractKey] = s.[ContractKey]
    WHEN MATCHED THEN
        UPDATE SET [ContractValue] = s.[ContractValue], [UpdatedAt] = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT ([ContractKey], [ContractValue]) VALUES (s.[ContractKey], s.[ContractValue]);
END
GO

-- Example grants (do not invent principals):
-- GRANT SELECT, EXECUTE ON SCHEMA::showcase TO [app_rw];
-- GRANT SELECT, EXECUTE ON SCHEMA::core TO [app_rw];
-- GRANT SELECT ON OBJECT::[deployment].[v_DatabaseContractHealth] TO [app_ro];
GO
