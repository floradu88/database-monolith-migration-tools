/*
Short-lived targeted troubleshooting only. Statement-level capture can be high volume.
Filter by application/login/database and stop after the investigation window.
*/
IF EXISTS (SELECT 1 FROM sys.server_event_sessions WHERE name = N'DbUsage_TargetedDeepDive')
    DROP EVENT SESSION [DbUsage_TargetedDeepDive] ON SERVER;
GO

CREATE EVENT SESSION [DbUsage_TargetedDeepDive] ON SERVER
ADD EVENT sqlserver.sp_statement_completed
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
    WHERE
    (
        sqlserver.database_name = N'REPLACE_DATABASE'
        AND sqlserver.client_app_name = N'REPLACE_APPLICATION'
    )
)
ADD TARGET package0.event_file
(
    SET filename = N'REPLACE_XEL_PATH_OR_AZURE_STORAGE_URL',
        max_file_size = 100,
        max_rollover_files = 5
)
WITH
(
    MAX_MEMORY = 16 MB,
    EVENT_RETENTION_MODE = ALLOW_SINGLE_EVENT_LOSS,
    MAX_DISPATCH_LATENCY = 10 SECONDS,
    TRACK_CAUSALITY = ON,
    STARTUP_STATE = OFF
);
GO
