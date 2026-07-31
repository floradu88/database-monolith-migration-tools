/*
Assumes telemetry.DatabaseObjectUsageHourly and ownership metadata.
Shows calls to objects whose migration state is cut over or deprecated.
Adapt object/column names to the catalog implementation.
*/
SELECT
    o.DatabaseName,
    o.SchemaName,
    o.ObjectName,
    o.ObjectType,
    a.DisplayName AS ApplicationName,
    SUM(u.ExecutionCount) AS ExecutionCount,
    MAX(u.LastSeenUtc) AS LastSeenUtc,
    SUM(u.FailureCount) AS FailureCount
FROM telemetry.DatabaseObjectUsageHourly u
JOIN inventory.DatabaseObject o
  ON o.DatabaseObjectId = u.DatabaseObjectId
LEFT JOIN inventory.Application a
  ON a.ApplicationId = u.ApplicationId
JOIN ownership.ObjectOwnership ow
  ON ow.DatabaseObjectId = o.DatabaseObjectId
WHERE ow.DecisionStatus IN ('CutoverComplete','Deprecated','RemovalCandidate')
  AND u.UsageDateHour >= DATEADD(day,-30,SYSUTCDATETIME())
GROUP BY
    o.DatabaseName,
    o.SchemaName,
    o.ObjectName,
    o.ObjectType,
    a.DisplayName
ORDER BY ExecutionCount DESC;
