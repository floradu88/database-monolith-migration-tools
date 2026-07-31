IF SCHEMA_ID(N'deployment') IS NULL
    EXEC(N'CREATE SCHEMA deployment AUTHORIZATION dbo;');
GO

IF OBJECT_ID(N'deployment.ChangeLedger', N'U') IS NULL
BEGIN
    CREATE TABLE deployment.ChangeLedger
    (
        ChangeId nvarchar(100) NOT NULL PRIMARY KEY,
        ArtifactVersion nvarchar(100) NOT NULL,
        CommitHash nvarchar(100) NULL,
        ScriptName nvarchar(500) NOT NULL,
        ScriptSha256 char(64) NOT NULL,
        AppliedUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        AppliedBy nvarchar(300) NOT NULL DEFAULT ORIGINAL_LOGIN(),
        PipelineRunId nvarchar(200) NULL,
        MigrationWave nvarchar(100) NULL,
        Success bit NOT NULL,
        ValidationResultJson nvarchar(max) NULL,
        CONSTRAINT CK_ChangeLedger_Json
            CHECK (ValidationResultJson IS NULL OR ISJSON(ValidationResultJson)=1)
    );
END;
GO
