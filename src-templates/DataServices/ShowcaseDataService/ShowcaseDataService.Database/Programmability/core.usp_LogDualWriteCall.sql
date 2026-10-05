-- Ownership: SqlProject — log dual-write call outcomes.

CREATE PROCEDURE [core].[usp_LogDualWriteCall]
    @PairId int = NULL,
    @Operation nvarchar(128),
    @BusinessKey nvarchar(200),
    @CorrelationId uniqueidentifier = NULL,
    @DboSucceeded bit,
    @CoreSucceeded bit,
    @CoreTimedOut bit = 0,
    @DboDurationMs int = NULL,
    @CoreDurationMs int = NULL,
    @CoreError nvarchar(400) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT [core].[DualWriteCallLog] (
        [PairId], [Operation], [BusinessKey], [CorrelationId],
        [DboSucceeded], [CoreSucceeded], [CoreTimedOut],
        [DboDurationMs], [CoreDurationMs], [CoreError])
    VALUES (
        @PairId, @Operation, @BusinessKey, COALESCE(@CorrelationId, NEWSEQUENTIALID()),
        @DboSucceeded, @CoreSucceeded, @CoreTimedOut,
        @DboDurationMs, @CoreDurationMs, @CoreError);
END;
