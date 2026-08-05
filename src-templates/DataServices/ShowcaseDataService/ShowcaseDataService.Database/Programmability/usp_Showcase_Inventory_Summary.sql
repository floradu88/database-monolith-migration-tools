-- Ownership: SqlProject (ShowcaseDataService.Database)
-- Object: showcase.usp_Showcase_Inventory_Summary
-- Kind: StoredProcedure
-- Template: usp_Showcase_{ShowcaseReportArea}_{ShowcaseReportAction}
-- Tokens: ShowcaseReportArea=Inventory, ShowcaseReportAction=Summary

CREATE OR ALTER PROCEDURE [showcase].[usp_Showcase_Inventory_Summary]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        [Id],
        [Name],
        [Status],
        CAST('Owned-SP-Inventory-Summary' AS NVARCHAR(50)) AS [Source]
    FROM [showcase].[Items]
    WHERE [Id] = @Id;
END
GO
