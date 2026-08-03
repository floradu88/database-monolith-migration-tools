-- Ownership: SqlProject (ShowcaseDataService.Database)
-- Object: showcase.GetShowcaseSummary
-- Kind: StoredProcedure
-- Versioning: desired-state in this project (git + dacpac). Cutover waves live under Cutover/.
-- Depends on EF-owned table showcase.Items (deploy EF migrations before this SP in owned mode).

CREATE OR ALTER PROCEDURE [showcase].[GetShowcaseSummary]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        [Id],
        [Name],
        [Status],
        CAST('Owned-SP' AS NVARCHAR(50)) AS [Source]
    FROM [showcase].[Items]
    WHERE [Id] = @Id;
END
GO
