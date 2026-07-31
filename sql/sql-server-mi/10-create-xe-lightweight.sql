/*
SQL Server or Azure SQL Managed Instance server-scoped session.
Adjust file path/URL and filters. Test in non-production first.
Captures completed RPC/batches with caller attribution; no parameter-specific extraction.
*/
IF EXISTS (SELECT 1 FROM sys.server_event_sessions WHERE name = N'DbUsage_Lightweight')
    DROP EVENT SESSION [DbUsage_Lightweight] ON SERVER;
GO

CREATE EVENT SESSION [DbUsage_Lightweight] ON SERVER
ADD EVENT sqlserver.rpc_completed
(
    ACTION
    (
        sqlserver.client_app_name,
        sqlserver.client_hostname,
        sqlserver.database_name,
        sqlserver.server_principal_name,
        sqlserver.session_id,
        sqlserver.sql_text
    )
    WHERE (sqlserver.database_name = N'REPLACE_DATABASE')
),
ADD EVENT sqlserver.sql_batch_completed
(
    ACTION
    (
        sqlserver.client_app_name,
        sqlserver.client_hostname,
        sqlserver.database_name,
        sqlserver.server_principal_name,
        sqlserver.session_id,
        sqlserver.sql_text
    )
    WHERE (sqlserver.database_name = N'REPLACE_DATABASE')
)
ADD TARGET package0.event_file
(
    SET filename = N'REPLACE_XEL_PATH_OR_AZURE_STORAGE_URL',
        max_file_size = 100,
        max_rollover_files = 20
)
WITH
(
    MAX_MEMORY = 16 MB,
    EVENT_RETENTION_MODE = ALLOW_SINGLE_EVENT_LOSS,
    MAX_DISPATCH_LATENCY = 30 SECONDS,
    TRACK_CAUSALITY = ON,
    STARTUP_STATE = ON
);
GO

ALTER EVENT SESSION [DbUsage_Lightweight] ON SERVER STATE = START;
GO
