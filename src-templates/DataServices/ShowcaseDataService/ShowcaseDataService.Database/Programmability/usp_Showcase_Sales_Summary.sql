-- Ownership: SqlProject (ShowcaseDataService.Database)
-- Object: showcase.usp_Showcase_Sales_Summary
-- Kind: StoredProcedure
-- Template: usp_Showcase_{ShowcaseReportArea}_{ShowcaseReportAction}
-- Tokens: ShowcaseReportArea=Sales, ShowcaseReportAction=Summary

CREATE OR ALTER PROCEDURE [showcase].[usp_Showcase_Sales_Summary]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        [Id],
        [Name],
        [Status],
        CAST('Owned-SP-Sales-Summary' AS NVARCHAR(50)) AS [Source]
    FROM [showcase].[Items]
    WHERE [Id] = @Id;
END
GO
