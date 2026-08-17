/*
Emit CREATE TABLE / INDEX DDL to clone dbo.T → core.T (structure only, no data).
Default @Apply = 0 (print/return script). Set @Apply = 1 only after DBA review.
Skips foreign keys and triggers (cross-schema FKs are a migration blocker until owned).
Requires sql/common/40-create-core-schema.sql.
*/
SET XACT_ABORT ON;
GO

CREATE OR ALTER PROCEDURE core.usp_EmitCloneTableDdl
    @SourceSchema sysname,
    @SourceTable sysname,
    @TargetSchema sysname = N'core',
    @Apply bit = 0
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF SCHEMA_ID(@SourceSchema) IS NULL OR OBJECT_ID(QUOTENAME(@SourceSchema) + N'.' + QUOTENAME(@SourceTable), N'U') IS NULL
    BEGIN
        RAISERROR(N'Source table [%s].[%s] was not found.', 16, 1, @SourceSchema, @SourceTable);
        RETURN;
    END;

    IF SCHEMA_ID(@TargetSchema) IS NULL
    BEGIN
        RAISERROR(N'Target schema [%s] was not found. Deploy 40-create-core-schema.sql first.', 16, 1, @TargetSchema);
        RETURN;
    END;

    DECLARE @src nvarchar(512) = QUOTENAME(@SourceSchema) + N'.' + QUOTENAME(@SourceTable);
    DECLARE @tgt nvarchar(512) = QUOTENAME(@TargetSchema) + N'.' + QUOTENAME(@SourceTable);
    DECLARE @cols nvarchar(max) = N'';
    DECLARE @pk nvarchar(max) = N'';
    DECLARE @ddl nvarchar(max);

    SELECT @cols = STRING_AGG(
        QUOTENAME(c.name) + N' '
        + t.name
        + CASE
            WHEN t.name IN (N'nvarchar', N'nchar', N'varchar', N'char', N'binary', N'varbinary')
                THEN N'(' + CASE WHEN c.max_length = -1 THEN N'max' ELSE CONVERT(nvarchar(20),
                    CASE WHEN t.name IN (N'nvarchar', N'nchar') THEN c.max_length / 2 ELSE c.max_length END) END + N')'
            WHEN t.name IN (N'decimal', N'numeric')
                THEN N'(' + CONVERT(nvarchar(20), c.precision) + N',' + CONVERT(nvarchar(20), c.scale) + N')'
            WHEN t.name IN (N'datetime2', N'datetimeoffset', N'time')
                THEN N'(' + CONVERT(nvarchar(20), c.scale) + N')'
            ELSE N''
          END
        + CASE WHEN c.is_identity = 1 THEN N' IDENTITY(' + CONVERT(nvarchar(20), IDENT_SEED(@src)) + N',' + CONVERT(nvarchar(20), IDENT_INCR(@src)) + N')' ELSE N'' END
        + CASE WHEN c.is_nullable = 1 THEN N' NULL' ELSE N' NOT NULL' END
        + CASE WHEN dc.definition IS NOT NULL THEN N' CONSTRAINT ' + QUOTENAME(N'DF_' + @TargetSchema + N'_' + @SourceTable + N'_' + c.name) + N' DEFAULT ' + dc.definition ELSE N'' END,
        N',' + CHAR(13) + CHAR(10) + N'        ')
    FROM sys.columns c
    JOIN sys.types t ON t.user_type_id = c.user_type_id
    LEFT JOIN sys.default_constraints dc ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    WHERE c.object_id = OBJECT_ID(@src)
      AND c.is_computed = 0;

    SELECT @pk = N',' + CHAR(13) + CHAR(10) + N'        CONSTRAINT ' + QUOTENAME(N'PK_' + @TargetSchema + N'_' + @SourceTable)
        + N' PRIMARY KEY (' + STRING_AGG(QUOTENAME(c.name), N', ') WITHIN GROUP (ORDER BY ic.key_ordinal) + N')'
    FROM sys.indexes i
    JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
    JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
    WHERE i.object_id = OBJECT_ID(@src)
      AND i.is_primary_key = 1;

    SET @ddl = N'IF OBJECT_ID(N''' + @tgt + N''', N''U'') IS NULL' + CHAR(13) + CHAR(10)
        + N'BEGIN' + CHAR(13) + CHAR(10)
        + N'    CREATE TABLE ' + @tgt + N'(' + CHAR(13) + CHAR(10)
        + N'        ' + @cols + ISNULL(@pk, N'') + CHAR(13) + CHAR(10)
        + N'    );' + CHAR(13) + CHAR(10)
        + N'END;';

    SELECT @ddl AS CloneTableDdl, @Apply AS WouldApply;

    IF @Apply = 1
        EXEC sys.sp_executesql @ddl;

    -- Non-PK indexes (additive).
    DECLARE @idx sysname, @unique bit, @idxCols nvarchar(max), @idxSql nvarchar(max);
    DECLARE idx_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT i.name, i.is_unique
        FROM sys.indexes i
        WHERE i.object_id = OBJECT_ID(@src)
          AND i.is_primary_key = 0
          AND i.is_unique_constraint = 0
          AND i.type_desc IN (N'CLUSTERED', N'NONCLUSTERED')
          AND i.name IS NOT NULL;

    OPEN idx_cursor;
    FETCH NEXT FROM idx_cursor INTO @idx, @unique;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SELECT @idxCols = STRING_AGG(QUOTENAME(c.name), N', ') WITHIN GROUP (ORDER BY ic.key_ordinal)
        FROM sys.index_columns ic
        JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
        WHERE ic.object_id = OBJECT_ID(@src) AND ic.index_id = (SELECT index_id FROM sys.indexes WHERE object_id = OBJECT_ID(@src) AND name = @idx)
          AND ic.is_included_column = 0;

        SET @idxSql = N'IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''' + @tgt + N''') AND name = N'''
            + REPLACE(N'IX_' + @TargetSchema + N'_' + @SourceTable + N'_' + @idx, '''', '''''') + N''')'
            + N' CREATE ' + CASE WHEN @unique = 1 THEN N'UNIQUE ' ELSE N'' END
            + N'NONCLUSTERED INDEX ' + QUOTENAME(N'IX_' + @TargetSchema + N'_' + @SourceTable + N'_' + @idx)
            + N' ON ' + @tgt + N' (' + @idxCols + N');';

        SELECT @idxSql AS CloneIndexDdl;
        IF @Apply = 1
            EXEC sys.sp_executesql @idxSql;

        FETCH NEXT FROM idx_cursor INTO @idx, @unique;
    END;
    CLOSE idx_cursor;
    DEALLOCATE idx_cursor;
END;
GO
