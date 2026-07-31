IF SCHEMA_ID(N'inventory') IS NULL
    EXEC(N'CREATE SCHEMA inventory AUTHORIZATION dbo;');
GO

IF OBJECT_ID(N'inventory.PermissionSnapshot', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.PermissionSnapshot
    (
        SnapshotId bigint IDENTITY PRIMARY KEY,
        CapturedUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        PrincipalName sysname NOT NULL,
        PrincipalType nvarchar(60) NOT NULL,
        PermissionState nvarchar(60) NOT NULL,
        PermissionName sysname NOT NULL,
        ClassDesc nvarchar(60) NOT NULL,
        SchemaName sysname NULL,
        ObjectName sysname NULL,
        CapturedBy nvarchar(300) NOT NULL DEFAULT ORIGINAL_LOGIN()
    );
END;
GO

INSERT inventory.PermissionSnapshot
(
    PrincipalName,
    PrincipalType,
    PermissionState,
    PermissionName,
    ClassDesc,
    SchemaName,
    ObjectName
)
SELECT
    pr.name,
    pr.type_desc,
    pe.state_desc,
    pe.permission_name,
    pe.class_desc,
    s.name,
    o.name
FROM sys.database_permissions pe
JOIN sys.database_principals pr
    ON pr.principal_id = pe.grantee_principal_id
LEFT JOIN sys.objects o
    ON pe.class = 1 AND pe.major_id = o.object_id
LEFT JOIN sys.schemas s
    ON s.schema_id = COALESCE(o.schema_id,
        CASE WHEN pe.class = 3 THEN pe.major_id END);
