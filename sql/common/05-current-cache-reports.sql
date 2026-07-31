SET NOCOUNT ON;

-- Current cached stored-procedure statistics. Not historical by itself.
SELECT
    OBJECT_SCHEMA_NAME(ps.object_id, ps.database_id) AS SchemaName,
    OBJECT_NAME(ps.object_id, ps.database_id) AS ProcedureName,
    ps.cached_time,
    ps.last_execution_time,
    ps.execution_count,
    ps.total_elapsed_time,
    ps.total_worker_time,
    ps.total_logical_reads,
    ps.total_logical_writes
FROM sys.dm_exec_procedure_stats AS ps
WHERE ps.database_id = DB_ID()
ORDER BY ps.last_execution_time DESC;

-- Current cached scalar-function statistics. Inline TVFs are not reliably represented here.
SELECT
    OBJECT_SCHEMA_NAME(fs.object_id, fs.database_id) AS SchemaName,
    OBJECT_NAME(fs.object_id, fs.database_id) AS FunctionName,
    fs.cached_time,
    fs.last_execution_time,
    fs.execution_count,
    fs.total_elapsed_time,
    fs.total_worker_time,
    fs.total_logical_reads,
    fs.total_logical_writes
FROM sys.dm_exec_function_stats AS fs
WHERE fs.database_id = DB_ID()
ORDER BY fs.last_execution_time DESC;

-- Objects absent from the current procedure/function cache: investigation list, NOT deletion list.
SELECT s.name AS SchemaName, o.name AS ObjectName, o.type_desc
FROM sys.objects AS o
JOIN sys.schemas AS s ON s.schema_id = o.schema_id
WHERE o.is_ms_shipped = 0
  AND o.type IN ('P','FN','FS','FT','IF','TF')
  AND NOT EXISTS
  (
      SELECT 1 FROM sys.dm_exec_procedure_stats ps
      WHERE ps.database_id = DB_ID() AND ps.object_id = o.object_id
  )
  AND NOT EXISTS
  (
      SELECT 1 FROM sys.dm_exec_function_stats fs
      WHERE fs.database_id = DB_ID() AND fs.object_id = o.object_id
  )
ORDER BY o.type_desc, s.name, o.name;
