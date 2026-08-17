/*
Template: clone a dbo stored procedure to core, pointing at core tables only.
Do not overwrite dbo.usp_*. Callers keep dbo until cutover.
DBA must paste the reviewed body; this file is not executable as-is.
Same parameters and result shape as the source procedure.

Example after review:

    CREATE OR ALTER PROCEDURE [core].[usp_Example]
        @ExternalId uniqueidentifier,
        @Name nvarchar(200),
        @Status nvarchar(50)
    AS
    BEGIN
        SET NOCOUNT ON;
        SET XACT_ABORT ON;
        -- Same DML as dbo.usp_Example, but every table is [core].*
        MERGE [core].[Example] AS t
        USING (SELECT @ExternalId AS ExternalId, @Name AS Name, @Status AS Status) AS s
        ON t.ExternalId = s.ExternalId
        WHEN MATCHED THEN UPDATE SET Name = s.Name, Status = s.Status, UpdatedAt = SYSUTCDATETIME()
        WHEN NOT MATCHED THEN INSERT (ExternalId, Name, Status, UpdatedAt)
            VALUES (s.ExternalId, s.Name, s.Status, SYSUTCDATETIME());
    END;

Runtime: the application (ParallelWrite route) or an optional sequential wrapper calls
dbo.usp_* and core.usp_* independently. dbo remains the caller result. core failures
are evidence only (see 42 / 44). Do not wrap both writes in one distributed transaction.
*/
SELECT
    N'Replace dbo schema qualifiers with core. Keep parameter list identical.' AS Instruction,
    N'sql/common/40-create-core-schema.sql' AS Prerequisite,
    N'sql/common/41-clone-table-to-core.sql' AS TableClone,
    N'src-templates/FindingsMigration generate-sp --parallel-dbo-core' AS Generator;
GO
