SELECT
    OBJECT_SCHEMA_NAME(q.object_id) AS schema_name,
    OBJECT_NAME(q.object_id) AS object_name,
    q.query_id,
    p.plan_id,
    SUM(rs.count_executions) AS execution_count,
    CAST(SUM(CONVERT(decimal(38,4),rs.avg_duration)*rs.count_executions)
         / NULLIF(SUM(rs.count_executions),0) / 1000.0 AS decimal(18,2))
         AS weighted_avg_duration_ms,
    MAX(rs.max_duration)/1000.0 AS max_duration_ms,
    CAST(SUM(CONVERT(decimal(38,4),rs.avg_cpu_time)*rs.count_executions)
         / NULLIF(SUM(rs.count_executions),0) / 1000.0 AS decimal(18,2))
         AS weighted_avg_cpu_ms,
    CAST(SUM(CONVERT(decimal(38,4),rs.avg_logical_io_reads)*rs.count_executions)
         / NULLIF(SUM(rs.count_executions),0) AS decimal(18,2))
         AS weighted_avg_logical_reads,
    MIN(rsi.start_time) AS first_interval_start,
    MAX(rsi.end_time) AS last_interval_end
FROM sys.query_store_query q
JOIN sys.query_store_plan p ON p.query_id=q.query_id
JOIN sys.query_store_runtime_stats rs ON rs.plan_id=p.plan_id
JOIN sys.query_store_runtime_stats_interval rsi
  ON rsi.runtime_stats_interval_id=rs.runtime_stats_interval_id
WHERE q.object_id<>0
GROUP BY q.object_id,q.query_id,p.plan_id
ORDER BY weighted_avg_duration_ms DESC;
