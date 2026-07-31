/* Replace path/URL wildcard. */
WITH EventData AS
(
    SELECT CAST(event_data AS xml) AS EventXml
    FROM sys.fn_xe_file_target_read_file
    (
        N'REPLACE_XEL_PATH_OR_URL*.xel', NULL, NULL, NULL
    )
)
SELECT
    EventXml.value('(event/@timestamp)[1]', 'datetime2') AS EventUtc,
    EventXml.value('(event/@name)[1]', 'sysname') AS EventName,
    EventXml.value('(event/action[@name="database_name"]/value)[1]', 'sysname') AS DatabaseName,
    EventXml.value('(event/action[@name="client_app_name"]/value)[1]', 'nvarchar(256)') AS ClientApplication,
    EventXml.value('(event/action[@name="client_hostname"]/value)[1]', 'nvarchar(256)') AS ClientHost,
    EventXml.value('(event/action[@name="server_principal_name"]/value)[1]', 'nvarchar(256)') AS LoginName,
    EventXml.value('(event/action[@name="session_id"]/value)[1]', 'int') AS SessionId,
    EventXml.value('(event/data[@name="duration"]/value)[1]', 'bigint') AS Duration,
    EventXml.value('(event/data[@name="cpu_time"]/value)[1]', 'bigint') AS CpuTime,
    EventXml.value('(event/data[@name="logical_reads"]/value)[1]', 'bigint') AS LogicalReads,
    EventXml.value('(event/data[@name="writes"]/value)[1]', 'bigint') AS Writes,
    EventXml.value('(event/action[@name="sql_text"]/value)[1]', 'nvarchar(max)') AS SqlText
FROM EventData
ORDER BY EventUtc DESC;
