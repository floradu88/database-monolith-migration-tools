/*
Purpose: Read-only preflight. Run in the target application database.
Review output before enabling tracking.
*/
SET NOCOUNT ON;

SELECT
    @@SERVERNAME AS ServerName,
    DB_NAME() AS DatabaseName,
    CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128)) AS ProductVersion,
    CAST(SERVERPROPERTY('ProductLevel') AS nvarchar(128)) AS ProductLevel,
    CAST(SERVERPROPERTY('Edition') AS nvarchar(128)) AS Edition,
    CAST(SERVERPROPERTY('EngineEdition') AS int) AS EngineEdition,
    compatibility_level,
    is_query_store_on
FROM sys.databases
WHERE database_id = DB_ID();

SELECT name, value, value_for_secondary
FROM sys.database_scoped_configurations
WHERE name IN
(
    'EXEC_QUERY_STATS_FOR_SCALAR_FUNCTIONS',
    'TSQL_SCALAR_UDF_INLINING'
);

SELECT
    actual_state_desc,
    desired_state_desc,
    readonly_reason,
    current_storage_size_mb,
    max_storage_size_mb,
    query_capture_mode_desc,
    size_based_cleanup_mode_desc,
    stale_query_threshold_days
FROM sys.database_query_store_options;

SELECT
    ORIGINAL_LOGIN() AS OriginalLogin,
    SUSER_SNAME() AS LoginName,
    USER_NAME() AS DatabaseUser,
    APP_NAME() AS ClientApplication,
    HOST_NAME() AS ClientHost;
