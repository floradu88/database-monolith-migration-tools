-- Ownership: SqlProject
CREATE OR ALTER PROCEDURE [dbo].[usp_ShowcaseWorkItem_Delete]
    @ExternalId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DELETE FROM [dbo].[ShowcaseWorkItem] WHERE [ExternalId] = @ExternalId;
END;
GO
