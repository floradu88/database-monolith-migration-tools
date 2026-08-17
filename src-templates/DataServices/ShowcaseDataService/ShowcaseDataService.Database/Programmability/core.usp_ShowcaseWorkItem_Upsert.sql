-- Ownership: SqlProject — behavior clone of dbo.usp_ShowcaseWorkItem_Upsert (core tables only).
CREATE OR ALTER PROCEDURE [core].[usp_ShowcaseWorkItem_Upsert]
    @ExternalId uniqueidentifier,
    @Name nvarchar(200),
    @Status nvarchar(50)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    MERGE [core].[ShowcaseWorkItem] AS t
    USING (SELECT @ExternalId AS [ExternalId], @Name AS [Name], @Status AS [Status]) AS s
    ON t.[ExternalId] = s.[ExternalId]
    WHEN MATCHED THEN
        UPDATE SET [Name] = s.[Name], [Status] = s.[Status], [UpdatedAt] = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT ([ExternalId], [Name], [Status], [UpdatedAt])
        VALUES (s.[ExternalId], s.[Name], s.[Status], SYSUTCDATETIME());
END;
GO
