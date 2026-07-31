WITH TargetData AS
(
    SELECT CAST(t.target_data AS xml) AS TargetXml
    FROM sys.dm_xe_database_session_targets AS t
    JOIN sys.dm_xe_database_sessions AS s
      ON s.address = t.event_session_address
    WHERE s.name = N'DbUsage_Lightweight'
      AND t.target_name = N'ring_buffer'
),
Events AS
(
    SELECT E.EventXml
    FROM TargetData
    CROSS APPLY TargetXml.nodes('/RingBufferTarget/event') AS E(EventXml)
)
SELECT
    EventXml.value('(@timestamp)[1]', 'datetime2') AS EventUtc,
    EventXml.value('(@name)[1]', 'sysname') AS EventName,
    EventXml.value('(action[@name="database_name"]/value)[1]', 'sysname') AS DatabaseName,
    EventXml.value('(action[@name="client_app_name"]/value)[1]', 'nvarchar(256)') AS ClientApplication,
    EventXml.value('(action[@name="client_hostname"]/value)[1]', 'nvarchar(256)') AS ClientHost,
    EventXml.value('(action[@name="server_principal_name"]/value)[1]', 'nvarchar(256)') AS LoginName,
    EventXml.value('(action[@name="session_id"]/value)[1]', 'int') AS SessionId,
    EventXml.value('(data[@name="duration"]/value)[1]', 'bigint') AS Duration,
    EventXml.value('(data[@name="cpu_time"]/value)[1]', 'bigint') AS CpuTime,
    EventXml.value('(data[@name="logical_reads"]/value)[1]', 'bigint') AS LogicalReads,
    EventXml.value('(data[@name="writes"]/value)[1]', 'bigint') AS Writes,
    EventXml.value('(action[@name="sql_text"]/value)[1]', 'nvarchar(max)') AS SqlText
FROM Events
ORDER BY EventUtc DESC;
