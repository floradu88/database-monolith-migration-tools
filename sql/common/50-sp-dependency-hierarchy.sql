SET NOCOUNT ON;
SET XACT_ABORT ON;

/*
Expected parameter:
  @SpName nvarchar(256) = 'schema.procName'

Returns multiple result sets (in a fixed order) intended to be packaged into an
inventory JSON snapshot by PowerShell tooling:

  1) ProcedureEdges
     - ParentProcedureFqn
     - ChildProcedureFqn
     - ChildDepth

  2) TableColumnUsage
     - ProcedureFqn
     - TableFqn
     - ColumnName
     - IsUsed (bit)

  3) TypeDependencies
     - ProcedureFqn
     - TypeFqn

  4) ViewDependencies
     - ProcedureFqn
     - ViewFqn

  5) FunctionDependencies
     - ProcedureFqn
     - FunctionFqn
     - FunctionType

Notes:
  - SQL is read-only catalog access. Review before executing in any environment.
  - Column usage for each table is derived via sys.dm_sql_referenced_entities.
  - Unused columns are computed by taking the full column list from sys.tables and
    subtracting referenced columns.
*/

DECLARE @RootObjectId int = OBJECT_ID(@SpName, 'P');

IF (@RootObjectId IS NULL)
BEGIN
    RAISERROR('Root stored procedure not found: %s', 16, 1, @SpName);
    RETURN;
END;

;WITH
ProcClosure AS
(
    -- Each row is one reachable stored procedure node with its parent edge + depth.
    SELECT
        @RootObjectId AS ProcObjectId,
        CAST(NULL AS int) AS ParentProcObjectId,
        0 AS Depth

    UNION ALL

    SELECT
        dep.referenced_id AS ProcObjectId,
        dep.referencing_id AS ParentProcObjectId,
        pc.Depth + 1 AS Depth
    FROM ProcClosure pc
    JOIN sys.sql_expression_dependencies dep
        ON dep.referencing_id = pc.ProcObjectId
    JOIN sys.objects child
        ON child.object_id = dep.referenced_id
    WHERE dep.referenced_id IS NOT NULL
      AND child.type IN ('P','PC','FN','FS','FT','IF','TF')  -- stored procedures + functions
      AND pc.Depth < 20              -- safety guard against unusual graphs
),
ProcNodes AS
(
    SELECT
        ProcObjectId,
        MIN(Depth) AS MinDepth
    FROM ProcClosure
    GROUP BY ProcObjectId
)
SELECT
    QUOTENAME(OBJECT_SCHEMA_NAME(pc.ParentProcObjectId)) + '.' + QUOTENAME(OBJECT_NAME(pc.ParentProcObjectId)) AS ParentProcedureFqn,
    QUOTENAME(OBJECT_SCHEMA_NAME(pc.ProcObjectId)) + '.' + QUOTENAME(OBJECT_NAME(pc.ProcObjectId)) AS ChildProcedureFqn,
    pc.Depth AS ChildDepth
FROM ProcClosure pc
WHERE pc.ParentProcObjectId IS NOT NULL
GROUP BY
    pc.ParentProcObjectId,
    pc.ProcObjectId,
    pc.Depth;

/*
Result set 2: TableColumnUsage
*/
;WITH
ProcNodeFqn AS
(
    SELECT
        pn.ProcObjectId,
        pn.MinDepth,
        QUOTENAME(OBJECT_SCHEMA_NAME(pn.ProcObjectId)) + '.' + QUOTENAME(OBJECT_NAME(pn.ProcObjectId)) AS ProcedureFqn
    FROM ProcNodes pn
),
UsedColumns AS
(
    SELECT
        p.ProcedureFqn,
        t.object_id AS TableObjectId,
        QUOTENAME(OBJECT_SCHEMA_NAME(t.object_id)) + '.' + QUOTENAME(t.name) AS TableFqn,
        rc.referenced_minor_name AS ColumnName
    FROM ProcNodeFqn p
    CROSS APPLY sys.dm_sql_referenced_entities(p.ProcedureFqn, 'OBJECT') AS rc
    JOIN sys.tables t
        ON t.name = rc.referenced_entity_name
       AND t.schema_id = SCHEMA_ID(rc.referenced_schema_name)
    WHERE rc.referenced_minor_name IS NOT NULL
),
UsedColumnsDistinct AS
(
    SELECT DISTINCT
        ProcedureFqn,
        TableObjectId,
        TableFqn,
        ColumnName
    FROM UsedColumns
),
UsedTables AS
(
    SELECT DISTINCT
        ProcedureFqn,
        TableObjectId,
        TableFqn
    FROM UsedColumnsDistinct
),
AllTableColumns AS
(
    SELECT
        ut.ProcedureFqn,
        ut.TableObjectId,
        ut.TableFqn,
        c.name AS ColumnName
    FROM UsedTables ut
    JOIN sys.columns c
        ON c.object_id = ut.TableObjectId
)
SELECT
    atc.ProcedureFqn,
    atc.TableFqn,
    atc.ColumnName,
    CASE WHEN ucd.ColumnName IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END AS IsUsed
FROM AllTableColumns atc
LEFT JOIN UsedColumnsDistinct ucd
    ON ucd.ProcedureFqn = atc.ProcedureFqn
   AND ucd.TableObjectId = atc.TableObjectId
   AND ucd.ColumnName = atc.ColumnName
ORDER BY
    atc.ProcedureFqn,
    atc.TableFqn,
    atc.ColumnName;

/*
Result set 3: TypeDependencies (user-defined types referenced by any procedure in the closure)
*/
;WITH
ProcNodesSimple AS
(
    SELECT
        pn.ProcObjectId,
        QUOTENAME(OBJECT_SCHEMA_NAME(pn.ProcObjectId)) + '.' + QUOTENAME(OBJECT_NAME(pn.ProcObjectId)) AS ProcedureFqn
    FROM ProcNodes pn
)
SELECT DISTINCT
    p.ProcedureFqn,
    QUOTENAME(OBJECT_SCHEMA_NAME(t.schema_id)) + '.' + QUOTENAME(t.name) AS TypeFqn
FROM ProcNodesSimple p
JOIN sys.sql_expression_dependencies dep
    ON dep.referencing_id = p.ProcObjectId
JOIN sys.types t
    ON t.user_type_id = dep.referenced_id
WHERE t.is_user_defined = 1;

/*
Result set 4: ViewDependencies
*/
;WITH
ProcNodesSimple2 AS
(
    SELECT
        pn.ProcObjectId,
        QUOTENAME(OBJECT_SCHEMA_NAME(pn.ProcObjectId)) + '.' + QUOTENAME(OBJECT_NAME(pn.ProcObjectId)) AS ProcedureFqn
    FROM ProcNodes pn
)
SELECT DISTINCT
    p.ProcedureFqn,
    QUOTENAME(OBJECT_SCHEMA_NAME(v.object_id)) + '.' + QUOTENAME(v.name) AS ViewFqn
FROM ProcNodesSimple2 p
JOIN sys.sql_expression_dependencies dep
    ON dep.referencing_id = p.ProcObjectId
JOIN sys.views v
    ON v.object_id = dep.referenced_id;

/*
Result set 5: FunctionDependencies (scalar/table-valued functions referenced but not in the procedure closure)
*/
;WITH
ProcNodesSimple3 AS
(
    SELECT
        pn.ProcObjectId,
        QUOTENAME(OBJECT_SCHEMA_NAME(pn.ProcObjectId)) + '.' + QUOTENAME(OBJECT_NAME(pn.ProcObjectId)) AS ProcedureFqn
    FROM ProcNodes pn
)
SELECT DISTINCT
    p.ProcedureFqn,
    QUOTENAME(OBJECT_SCHEMA_NAME(f.object_id)) + '.' + QUOTENAME(f.name) AS FunctionFqn,
    f.type_desc AS FunctionType
FROM ProcNodesSimple3 p
JOIN sys.sql_expression_dependencies dep
    ON dep.referencing_id = p.ProcObjectId
JOIN sys.objects f
    ON f.object_id = dep.referenced_id
WHERE f.type IN ('FN','FS','FT','IF','TF');

