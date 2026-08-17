-- Ownership: SqlProject — behavior clone of dbo.usp_ShowcaseWorkItem_Delete.
CREATE OR ALTER PROCEDURE [core].[usp_ShowcaseWorkItem_Delete]
    @ExternalId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DELETE FROM [core].[ShowcaseWorkItem] WHERE [ExternalId] = @ExternalId;
END;
GO
