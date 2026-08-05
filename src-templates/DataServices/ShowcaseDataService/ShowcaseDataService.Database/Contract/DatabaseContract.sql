-- Ownership: SqlProject (ShowcaseDataService.Database)
-- Purpose: schema/contract version surface for health + CI drift checks.
-- Deployed via dacpac desired-state; stamped by PostDeploy.
-- Idempotent for lab re-apply via sqlcmd (CREATE TABLE IF NOT EXISTS pattern).

IF OBJECT_ID(N'[deployment].[DatabaseContract]', N'U') IS NULL
BEGIN
    CREATE TABLE [deployment].[DatabaseContract]
    (
        [ContractKey] NVARCHAR(100) NOT NULL PRIMARY KEY,
        [ContractValue] NVARCHAR(200) NOT NULL,
        [UpdatedAt] DATETIMEOFFSET NOT NULL
            CONSTRAINT [DF_DatabaseContract_UpdatedAt] DEFAULT (SYSUTCDATETIME())
    );
END
GO

CREATE OR ALTER VIEW [deployment].[v_DatabaseContractHealth]
AS
    SELECT
        [ContractKey],
        [ContractValue],
        [UpdatedAt]
    FROM [deployment].[DatabaseContract];
GO
