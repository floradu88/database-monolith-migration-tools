/*
Run in the monitored database if telemetry tables are local.
If using a central catalog, execute SELECT portions remotely and insert through the collector.
Schedule every 5-15 minutes. DMVs are cache-based; snapshots preserve observations.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @SnapshotUtc datetime2(3) = SYSUTCDATETIME();

INSERT telemetry.ProcedureStatsSnapshot
(
    SnapshotUtc, DatabaseName, SchemaName, ObjectName, ObjectId,
    PlanHandle, CachedTime, LastExecutionTime, ExecutionCount,
    TotalWorkerTime, TotalElapsedTime, TotalLogicalReads, TotalLogicalWrites
)
SELECT
    @SnapshotUtc,
    DB_NAME(ps.database_id),
    OBJECT_SCHEMA_NAME(ps.object_id, ps.database_id),
    OBJECT_NAME(ps.object_id, ps.database_id),
    ps.object_id,
    ps.plan_handle,
    ps.cached_time,
    ps.last_execution_time,
    ps.execution_count,
    ps.total_worker_time,
    ps.total_elapsed_time,
    ps.total_logical_reads,
    ps.total_logical_writes
FROM sys.dm_exec_procedure_stats AS ps
WHERE ps.database_id = DB_ID();

INSERT telemetry.FunctionStatsSnapshot
(
    SnapshotUtc, DatabaseName, SchemaName, ObjectName, ObjectId,
    PlanHandle, CachedTime, LastExecutionTime, ExecutionCount,
    TotalWorkerTime, TotalElapsedTime, TotalLogicalReads, TotalLogicalWrites
)
SELECT
    @SnapshotUtc,
    DB_NAME(fs.database_id),
    OBJECT_SCHEMA_NAME(fs.object_id, fs.database_id),
    OBJECT_NAME(fs.object_id, fs.database_id),
    fs.object_id,
    fs.plan_handle,
    fs.cached_time,
    fs.last_execution_time,
    fs.execution_count,
    fs.total_worker_time,
    fs.total_elapsed_time,
    fs.total_logical_reads,
    fs.total_logical_writes
FROM sys.dm_exec_function_stats AS fs
WHERE fs.database_id = DB_ID();

INSERT telemetry.TriggerStatsSnapshot
(
    SnapshotUtc, DatabaseName, SchemaName, ObjectName, ObjectId,
    ExecutionCount, LastExecutionTime, TotalWorkerTime, TotalElapsedTime
)
SELECT
    @SnapshotUtc,
    DB_NAME(ts.database_id),
    OBJECT_SCHEMA_NAME(ts.object_id, ts.database_id),
    OBJECT_NAME(ts.object_id, ts.database_id),
    ts.object_id,
    ts.execution_count,
    ts.last_execution_time,
    ts.total_worker_time,
    ts.total_elapsed_time
FROM sys.dm_exec_trigger_stats AS ts
WHERE ts.database_id = DB_ID();
