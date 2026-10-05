-- Ownership: SqlProject (ShowcaseDataService.Database)

CREATE VIEW [deployment].[v_DatabaseContractHealth]
AS
    SELECT
        [ContractKey],
        [ContractValue],
        [UpdatedAt]
    FROM [deployment].[DatabaseContract];
