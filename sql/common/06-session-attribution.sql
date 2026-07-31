/*
Execute once after opening each application connection.
Use a shared .NET connection factory or EF Core DbConnectionInterceptor.
Do not trust client-provided values for authorization decisions; these values are telemetry context.
*/
DECLARE @ApplicationName nvarchar(128) = N'REPLACE_APPLICATION';
DECLARE @ApplicationVersion nvarchar(64) = N'REPLACE_VERSION';
DECLARE @Environment nvarchar(32) = N'Production';
DECLARE @CorrelationId nvarchar(64) = CONVERT(nvarchar(64), NEWID());

EXEC sys.sp_set_session_context @key=N'ApplicationName', @value=@ApplicationName, @read_only=1;
EXEC sys.sp_set_session_context @key=N'ApplicationVersion', @value=@ApplicationVersion, @read_only=1;
EXEC sys.sp_set_session_context @key=N'Environment', @value=@Environment, @read_only=1;
EXEC sys.sp_set_session_context @key=N'CorrelationId', @value=@CorrelationId;

SELECT
    APP_NAME() AS ClientApplicationName,
    SESSION_CONTEXT(N'ApplicationName') AS SessionApplicationName,
    SESSION_CONTEXT(N'ApplicationVersion') AS ApplicationVersion,
    SESSION_CONTEXT(N'Environment') AS Environment,
    SESSION_CONTEXT(N'CorrelationId') AS CorrelationId;
