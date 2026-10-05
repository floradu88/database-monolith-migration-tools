-- Ownership: SqlProject (ShowcaseDataService.Database)
-- Purpose: schema/contract version surface for health + CI drift checks.
-- Deployed via dacpac desired-state; stamped by PostDeploy.

CREATE TABLE [deployment].[DatabaseContract]
(
    [ContractKey] NVARCHAR(100) NOT NULL PRIMARY KEY,
    [ContractValue] NVARCHAR(200) NOT NULL,
    [UpdatedAt] DATETIMEOFFSET NOT NULL
        CONSTRAINT [DF_DatabaseContract_UpdatedAt] DEFAULT (SYSUTCDATETIME())
);
