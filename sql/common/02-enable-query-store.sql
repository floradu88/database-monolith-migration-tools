/*
Review values against workload and storage budget before production use.
Replace [YourDatabase] safely.
*/
DECLARE @DatabaseName sysname = DB_NAME();
DECLARE @Sql nvarchar(max);

SET @Sql = N'ALTER DATABASE ' + QUOTENAME(@DatabaseName) + N' SET QUERY_STORE = ON;';
EXEC sys.sp_executesql @Sql;

SET @Sql = N'ALTER DATABASE ' + QUOTENAME(@DatabaseName) + N' SET QUERY_STORE
(
    OPERATION_MODE = READ_WRITE,
    CLEANUP_POLICY = (STALE_QUERY_THRESHOLD_DAYS = 90),
    DATA_FLUSH_INTERVAL_SECONDS = 900,
    INTERVAL_LENGTH_MINUTES = 60,
    MAX_STORAGE_SIZE_MB = 2048,
    QUERY_CAPTURE_MODE = AUTO,
    SIZE_BASED_CLEANUP_MODE = AUTO
);';
EXEC sys.sp_executesql @Sql;

SELECT * FROM sys.database_query_store_options;
