IF SCHEMA_ID(N'inventory') IS NULL
    EXEC(N'CREATE SCHEMA inventory AUTHORIZATION dbo;');
GO

IF OBJECT_ID(N'inventory.ObjectDefinitionSnapshot', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.ObjectDefinitionSnapshot
    (
        SnapshotId bigint IDENTITY PRIMARY KEY,
        CapturedUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        DatabaseName sysname NOT NULL DEFAULT DB_NAME(),
        SchemaName sysname NOT NULL,
        ObjectName sysname NOT NULL,
        ObjectType nvarchar(60) NOT NULL,
        ObjectId int NOT NULL,
        Definition nvarchar(max) NULL,
        DefinitionSha256 varchar(64) NULL,
        CapturedBy nvarchar(300) NOT NULL DEFAULT ORIGINAL_LOGIN()
    );
END;
GO

INSERT inventory.ObjectDefinitionSnapshot
(
    SchemaName,
    ObjectName,
    ObjectType,
    ObjectId,
    Definition,
    DefinitionSha256
)
SELECT
    s.name,
    o.name,
    o.type_desc,
    o.object_id,
    m.definition,
    CONVERT(varchar(64),
        HASHBYTES('SHA2_256', CONVERT(varbinary(max), COALESCE(m.definition,N''))),
        2)
FROM sys.objects o
JOIN sys.schemas s ON s.schema_id = o.schema_id
LEFT JOIN sys.sql_modules m ON m.object_id = o.object_id
WHERE o.is_ms_shipped = 0;
