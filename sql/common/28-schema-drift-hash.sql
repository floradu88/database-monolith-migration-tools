SELECT
    s.name AS schema_name,
    o.name AS object_name,
    o.type_desc,
    CONVERT(varchar(64),
        HASHBYTES(
            'SHA2_256',
            CONVERT(varbinary(max), COALESCE(m.definition, N''))
        ),
        2) AS definition_sha256,
    o.modify_date
FROM sys.objects o
JOIN sys.schemas s ON s.schema_id = o.schema_id
LEFT JOIN sys.sql_modules m ON m.object_id = o.object_id
WHERE o.is_ms_shipped = 0
ORDER BY s.name, o.name;
