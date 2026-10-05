-- kind: ddl
-- Sample: prefer CREATE OR ALTER when dropping scripts from a SQL Server project into the inbox.
-- Remove or replace this file before pointing the tool at a database you care about.

CREATE OR ALTER PROCEDURE [dbo].[usp_MigrationTool_Example]
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT @Id AS Id;
END
