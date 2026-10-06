-- Run once in SSMS or Azure Data Studio.
-- Connect to the target database first (the Initial Catalog in MIGRATION_CONNECTION_STRING).
-- Do not drop this file into scripts/inbox.
-- The login needs CREATE SCHEMA (db_owner or db_ddladmin).

IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
BEGIN
    DECLARE @systemDb sysname = DB_NAME();
    RAISERROR('Refusing to create the journal in system database %s. Connect to the target database first.', 16, 1, @systemDb);
    RETURN;
END

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'migration')
    EXEC(N'CREATE SCHEMA [migration]');

IF OBJECT_ID(N'migration.schema_versions', N'U') IS NULL
    EXEC(N'
        CREATE TABLE [migration].[schema_versions] (
            [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_schema_versions_Id] PRIMARY KEY,
            [ScriptName] nvarchar(255) NOT NULL,
            [Applied] datetime NOT NULL
        )');

SELECT
    DB_NAME() AS TargetDatabase,
    SCHEMA_NAME(schema_id) AS JournalSchema,
    name AS JournalTable
FROM sys.tables
WHERE name = N'schema_versions'
  AND SCHEMA_NAME(schema_id) = N'migration';
