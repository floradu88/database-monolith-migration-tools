SELECT
    SYSUTCDATETIME() AS captured_utc,
    DB_NAME() AS database_name,
    SUM(size)*8.0/1024 AS allocated_size_mb,
    SUM(CASE WHEN type_desc='ROWS' THEN size ELSE 0 END)*8.0/1024 AS data_mb,
    SUM(CASE WHEN type_desc='LOG' THEN size ELSE 0 END)*8.0/1024 AS log_mb
FROM sys.database_files;

SELECT
    OBJECT_SCHEMA_NAME(i.object_id) AS schema_name,
    OBJECT_NAME(i.object_id) AS object_name,
    i.name AS index_name,
    SUM(ps.row_count) AS row_count,
    SUM(ps.reserved_page_count)*8.0/1024 AS reserved_mb,
    SUM(ps.used_page_count)*8.0/1024 AS used_mb
FROM sys.dm_db_partition_stats ps
LEFT JOIN sys.indexes i
  ON i.object_id=ps.object_id AND i.index_id=ps.index_id
GROUP BY i.object_id,i.name
ORDER BY reserved_mb DESC;
