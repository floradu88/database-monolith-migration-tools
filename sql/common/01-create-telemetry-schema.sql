/*
Deploy in a dedicated intelligence database when supported.
For Azure SQL Database, a collector can write to a separate catalog database;
if cross-database access is unavailable, deploy locally and export centrally.
*/
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF SCHEMA_ID(N'telemetry') IS NULL EXEC(N'CREATE SCHEMA telemetry AUTHORIZATION dbo;');
IF SCHEMA_ID(N'inventory') IS NULL EXEC(N'CREATE SCHEMA inventory AUTHORIZATION dbo;');

IF OBJECT_ID(N'inventory.DatabaseObject', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.DatabaseObject
    (
        DatabaseObjectId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_DatabaseObject PRIMARY KEY,
        ServerName nvarchar(256) NOT NULL,
        DatabaseName sysname NOT NULL,
        SchemaName sysname NOT NULL,
        ObjectName sysname NOT NULL,
        ObjectType varchar(10) NOT NULL,
        SqlObjectId int NULL,
        DefinitionHash varbinary(32) NULL,
        FirstDiscoveredUtc datetime2(3) NOT NULL CONSTRAINT DF_DatabaseObject_First DEFAULT SYSUTCDATETIME(),
        LastDiscoveredUtc datetime2(3) NOT NULL CONSTRAINT DF_DatabaseObject_Last DEFAULT SYSUTCDATETIME(),
        IsPresent bit NOT NULL CONSTRAINT DF_DatabaseObject_Present DEFAULT 1,
        CONSTRAINT UQ_DatabaseObject UNIQUE(ServerName, DatabaseName, SchemaName, ObjectName, ObjectType)
    );
END;

IF OBJECT_ID(N'telemetry.ProcedureStatsSnapshot', N'U') IS NULL
BEGIN
    CREATE TABLE telemetry.ProcedureStatsSnapshot
    (
        SnapshotUtc datetime2(3) NOT NULL,
        DatabaseName sysname NOT NULL,
        SchemaName sysname NOT NULL,
        ObjectName sysname NOT NULL,
        ObjectId int NOT NULL,
        PlanHandle varbinary(64) NULL,
        CachedTime datetime2 NULL,
        LastExecutionTime datetime2 NULL,
        ExecutionCount bigint NOT NULL,
        TotalWorkerTime bigint NULL,
        TotalElapsedTime bigint NULL,
        TotalLogicalReads bigint NULL,
        TotalLogicalWrites bigint NULL,
        CONSTRAINT PK_ProcedureStatsSnapshot PRIMARY KEY
            (SnapshotUtc, DatabaseName, ObjectId, PlanHandle)
    );
END;

IF OBJECT_ID(N'telemetry.FunctionStatsSnapshot', N'U') IS NULL
BEGIN
    CREATE TABLE telemetry.FunctionStatsSnapshot
    (
        SnapshotUtc datetime2(3) NOT NULL,
        DatabaseName sysname NOT NULL,
        SchemaName sysname NOT NULL,
        ObjectName sysname NOT NULL,
        ObjectId int NOT NULL,
        PlanHandle varbinary(64) NULL,
        CachedTime datetime2 NULL,
        LastExecutionTime datetime2 NULL,
        ExecutionCount bigint NOT NULL,
        TotalWorkerTime bigint NULL,
        TotalElapsedTime bigint NULL,
        TotalLogicalReads bigint NULL,
        TotalLogicalWrites bigint NULL,
        CONSTRAINT PK_FunctionStatsSnapshot PRIMARY KEY
            (SnapshotUtc, DatabaseName, ObjectId, PlanHandle)
    );
END;

IF OBJECT_ID(N'telemetry.TriggerStatsSnapshot', N'U') IS NULL
BEGIN
    CREATE TABLE telemetry.TriggerStatsSnapshot
    (
        SnapshotUtc datetime2(3) NOT NULL,
        DatabaseName sysname NOT NULL,
        SchemaName sysname NOT NULL,
        ObjectName sysname NOT NULL,
        ObjectId int NOT NULL,
        ExecutionCount bigint NOT NULL,
        LastExecutionTime datetime2 NULL,
        TotalWorkerTime bigint NULL,
        TotalElapsedTime bigint NULL,
        CONSTRAINT PK_TriggerStatsSnapshot PRIMARY KEY
            (SnapshotUtc, DatabaseName, ObjectId)
    );
END;


IF OBJECT_ID(N'inventory.Application', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.Application
    (
        ApplicationId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Application PRIMARY KEY,
        DisplayName nvarchar(256) NOT NULL,
        Environment nvarchar(100) NULL,
        SqlIdentity nvarchar(300) NULL,
        ConnectionApplicationName nvarchar(256) NULL,
        OwnerTeam nvarchar(256) NULL,
        IsActive bit NOT NULL CONSTRAINT DF_Application_IsActive DEFAULT 1,
        FirstDiscoveredUtc datetime2(3) NOT NULL CONSTRAINT DF_Application_First DEFAULT SYSUTCDATETIME(),
        LastDiscoveredUtc datetime2(3) NOT NULL CONSTRAINT DF_Application_Last DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_Application UNIQUE(DisplayName, Environment)
    );
END;

IF OBJECT_ID(N'telemetry.DatabaseObjectUsageHourly', N'U') IS NULL
BEGIN
    CREATE TABLE telemetry.DatabaseObjectUsageHourly
    (
        UsageDateHour datetime2(0) NOT NULL,
        DatabaseObjectId bigint NOT NULL,
        ApplicationId bigint NULL,
        ActionName varchar(30) NOT NULL,
        ExecutionCount bigint NOT NULL,
        FailureCount bigint NOT NULL CONSTRAINT DF_Usage_Failure DEFAULT 0,
        TotalDurationMs decimal(38,3) NULL,
        MaximumDurationMs decimal(19,3) NULL,
        TotalCpuMs decimal(38,3) NULL,
        TotalLogicalReads bigint NULL,
        TotalLogicalWrites bigint NULL,
        FirstSeenUtc datetime2(3) NOT NULL,
        LastSeenUtc datetime2(3) NOT NULL,
        AttributionConfidence decimal(5,4) NULL,
        CONSTRAINT PK_DatabaseObjectUsageHourly PRIMARY KEY
            (UsageDateHour, DatabaseObjectId, ApplicationId, ActionName),
        CONSTRAINT FK_Usage_Object FOREIGN KEY(DatabaseObjectId)
            REFERENCES inventory.DatabaseObject(DatabaseObjectId),
        CONSTRAINT FK_Usage_Application FOREIGN KEY(ApplicationId)
            REFERENCES inventory.Application(ApplicationId)
    );
END;

IF SCHEMA_ID(N'ownership') IS NULL EXEC(N'CREATE SCHEMA ownership AUTHORIZATION dbo;');

IF OBJECT_ID(N'ownership.ObjectOwnership', N'U') IS NULL
BEGIN
    CREATE TABLE ownership.ObjectOwnership
    (
        DatabaseObjectId bigint NOT NULL CONSTRAINT PK_ObjectOwnership PRIMARY KEY,
        OwnerDomain nvarchar(200) NULL,
        OwnerTeam nvarchar(256) NULL,
        TargetService nvarchar(256) NULL,
        TargetDatabase sysname NULL,
        DecisionStatus varchar(40) NOT NULL CONSTRAINT DF_Ownership_Status DEFAULT 'Discovered',
        Confidence decimal(5,4) NULL,
        EvidenceJson nvarchar(max) NULL,
        ApprovedBy nvarchar(300) NULL,
        ApprovedUtc datetime2(3) NULL,
        CONSTRAINT FK_Ownership_Object FOREIGN KEY(DatabaseObjectId)
            REFERENCES inventory.DatabaseObject(DatabaseObjectId),
        CONSTRAINT CK_Ownership_EvidenceJson CHECK(EvidenceJson IS NULL OR ISJSON(EvidenceJson)=1)
    );
END;

COMMIT TRANSACTION;
