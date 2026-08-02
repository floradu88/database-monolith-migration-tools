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
