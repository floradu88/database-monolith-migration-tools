-- Build an additive UPDATE TABLE script by comparing desired columns to the live table.
-- Run in SSMS / Azure Data Studio against the target database (not master).
-- Do not drop this file into scripts/inbox.
--
-- 1) Set @Schema and @Table.
-- 2) Insert the CREATE TABLE source columns into #DesiredColumns (name + SQL type).
-- 3) Review the Result grid (AlterStatement). That is the update script.
-- 4) Set @Execute = 1 only after review if you want this script to apply the ALTERs.

IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
BEGIN
    DECLARE @systemDb sysname = DB_NAME();
    RAISERROR('Refusing to generate table updates in system database %s. Connect to the target database first.', 16, 1, @systemDb);
    RETURN;
END

DECLARE @Schema sysname = N'dbo';
DECLARE @Table  sysname = N'City';
DECLARE @Execute bit = 0; -- set to 1 to run the generated ALTER statements after review

IF OBJECT_ID(N'tempdb..#DesiredColumns') IS NOT NULL
    DROP TABLE #DesiredColumns;

CREATE TABLE #DesiredColumns
(
    ColumnName sysname NOT NULL PRIMARY KEY,
    SqlType    nvarchar(256) NOT NULL
);

-- Replace these rows with the columns from your CREATE TABLE source.
INSERT INTO #DesiredColumns (ColumnName, SqlType) VALUES
    (N'Id',   N'int NOT NULL'),
    (N'Name', N'nvarchar(100) NULL'),
    (N'Code', N'nvarchar(10) NULL');

IF OBJECT_ID(QUOTENAME(@Schema) + N'.' + QUOTENAME(@Table), N'U') IS NULL
BEGIN
    RAISERROR('Live table %s.%s does not exist. Run CREATE TABLE first, or fix @Schema/@Table.', 16, 1, @Schema, @Table);
    RETURN;
END

;WITH LiveColumns AS
(
    SELECT c.name AS ColumnName
    FROM sys.columns AS c
    INNER JOIN sys.tables AS t ON t.object_id = c.object_id
    INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
    WHERE s.name = @Schema
      AND t.name = @Table
),
Missing AS
(
    SELECT d.ColumnName, d.SqlType
    FROM #DesiredColumns AS d
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM LiveColumns AS l
        WHERE l.ColumnName = d.ColumnName
    )
)
SELECT
    DB_NAME() AS TargetDatabase,
    @Schema AS TableSchema,
    @Table AS TableName,
    m.ColumnName,
    m.SqlType,
    N'ALTER TABLE ' + QUOTENAME(@Schema) + N'.' + QUOTENAME(@Table)
        + N' ADD ' + QUOTENAME(m.ColumnName) + N' ' + m.SqlType + N';' AS AlterStatement
FROM Missing AS m
ORDER BY m.ColumnName;

DECLARE @UpdateScript nvarchar(max) = N'';
SELECT @UpdateScript = @UpdateScript
    + N'ALTER TABLE ' + QUOTENAME(@Schema) + N'.' + QUOTENAME(@Table)
    + N' ADD ' + QUOTENAME(m.ColumnName) + N' ' + m.SqlType + N';'
    + CHAR(13) + CHAR(10)
FROM #DesiredColumns AS m
WHERE NOT EXISTS
(
    SELECT 1
    FROM sys.columns AS c
    INNER JOIN sys.tables AS t ON t.object_id = c.object_id
    INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
    WHERE s.name = @Schema
      AND t.name = @Table
      AND c.name = m.ColumnName
);

IF (@UpdateScript = N'')
BEGIN
    SELECT N'-- No ALTER statements required. Live table already has every desired column.' AS UpdateScript;
    RETURN;
END

SELECT @UpdateScript AS UpdateScript;

IF (@Execute = 1)
BEGIN
    PRINT N'Executing generated update script...';
    EXEC sys.sp_executesql @UpdateScript;
    PRINT N'Done.';
END
ELSE
BEGIN
    PRINT N'Review UpdateScript above. Set @Execute = 1 to apply it.';
END
