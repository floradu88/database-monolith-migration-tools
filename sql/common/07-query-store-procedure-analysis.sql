/* Query Store-oriented module report. Validate on your SQL Server version. */
SET NOCOUNT ON;

SELECT
    OBJECT_SCHEMA_NAME(q.object_id) AS SchemaName,
    OBJECT_NAME(q.object_id) AS ModuleName,
    o.type_desc AS ModuleType,
    SUM(rs.count_executions) AS ExecutionCount,
    MAX(rs.last_execution_time) AS LastExecutionTime,
    SUM(CONVERT(decimal(38,2), rs.avg_duration) * rs.count_executions)
        / NULLIF(SUM(rs.count_executions), 0) AS WeightedAverageDurationMicroseconds,
    SUM(CONVERT(decimal(38,2), rs.avg_cpu_time) * rs.count_executions)
        / NULLIF(SUM(rs.count_executions), 0) AS WeightedAverageCpuMicroseconds,
    SUM(CONVERT(decimal(38,2), rs.avg_logical_io_reads) * rs.count_executions)
        / NULLIF(SUM(rs.count_executions), 0) AS WeightedAverageLogicalReads
FROM sys.query_store_query AS q
JOIN sys.query_store_plan AS p ON p.query_id = q.query_id
JOIN sys.query_store_runtime_stats AS rs ON rs.plan_id = p.plan_id
LEFT JOIN sys.objects AS o ON o.object_id = q.object_id
WHERE q.object_id <> 0
GROUP BY q.object_id, o.type_desc
ORDER BY LastExecutionTime DESC;
