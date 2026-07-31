EXEC sys.sp_set_session_context
    @key = N'ApplicationName',
    @value = N'REPLACE_APPLICATION_NAME';

EXEC sys.sp_set_session_context
    @key = N'ApplicationVersion',
    @value = N'REPLACE_VERSION';

EXEC sys.sp_set_session_context
    @key = N'Environment',
    @value = N'REPLACE_ENVIRONMENT';

EXEC sys.sp_set_session_context
    @key = N'TraceId',
    @value = N'REPLACE_TRACE_ID';

EXEC sys.sp_set_session_context
    @key = N'ShardId',
    @value = N'REPLACE_SHARD_ID';
