/*
Azure SQL Database supports database-scoped event sessions.
An event_file target normally uses Azure Storage and a database scoped credential.
The ring_buffer target below is suitable for initial validation but is not a durable history store.
*/
IF EXISTS (SELECT 1 FROM sys.database_event_sessions WHERE name = N'DbUsage_Lightweight')
    DROP EVENT SESSION [DbUsage_Lightweight] ON DATABASE;
GO

CREATE EVENT SESSION [DbUsage_Lightweight] ON DATABASE
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
)
ADD TARGET package0.ring_buffer
(
    SET max_memory = 4096
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

ALTER EVENT SESSION [DbUsage_Lightweight] ON DATABASE STATE = START;
GO
