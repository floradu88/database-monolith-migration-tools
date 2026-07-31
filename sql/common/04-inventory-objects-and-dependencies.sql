SET NOCOUNT ON;

-- Modules and hashes
SELECT
    DB_NAME() AS DatabaseName,
    s.name AS SchemaName,
    o.name AS ObjectName,
    o.type,
    o.type_desc,
    o.object_id,
    o.create_date,
    o.modify_date,
    HASHBYTES('SHA2_256', CONVERT(varbinary(max), sm.definition)) AS DefinitionHash,
    sm.uses_ansi_nulls,
    sm.uses_quoted_identifier,
    sm.is_schema_bound,
    sm.execute_as_principal_id
FROM sys.objects AS o
JOIN sys.schemas AS s ON s.schema_id = o.schema_id
LEFT JOIN sys.sql_modules AS sm ON sm.object_id = o.object_id
WHERE o.is_ms_shipped = 0
  AND o.type IN ('P','PC','FN','FS','FT','IF','TF','V','TR');

-- Declared SQL dependencies; dynamic SQL and external callers require additional discovery.
SELECT
    OBJECT_SCHEMA_NAME(d.referencing_id) AS ReferencingSchema,
    OBJECT_NAME(d.referencing_id) AS ReferencingObject,
    ro.type_desc AS ReferencingType,
    d.referenced_server_name,
    d.referenced_database_name,
    d.referenced_schema_name,
    d.referenced_entity_name,
    d.referenced_id,
    d.is_schema_bound_reference,
    d.is_caller_dependent,
    d.is_ambiguous
FROM sys.sql_expression_dependencies AS d
LEFT JOIN sys.objects AS ro ON ro.object_id = d.referencing_id;

-- Dynamic SQL indicators
SELECT
    s.name AS SchemaName,
    o.name AS ObjectName,
    o.type_desc,
    CASE WHEN sm.definition LIKE '%sp_executesql%' THEN 1 ELSE 0 END AS UsesSpExecuteSql,
    CASE WHEN sm.definition LIKE '%EXEC(%' OR sm.definition LIKE '%EXECUTE(%' THEN 1 ELSE 0 END AS UsesExecString
FROM sys.sql_modules AS sm
JOIN sys.objects AS o ON o.object_id = sm.object_id
JOIN sys.schemas AS s ON s.schema_id = o.schema_id
WHERE sm.definition LIKE '%sp_executesql%'
   OR sm.definition LIKE '%EXEC(%'
   OR sm.definition LIKE '%EXECUTE(%';

-- Synonyms
SELECT
    s.name AS SchemaName,
    sy.name AS SynonymName,
    sy.base_object_name
FROM sys.synonyms AS sy
JOIN sys.schemas AS s ON s.schema_id = sy.schema_id;
