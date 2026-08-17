/*
Logical split foundation: dbo (legacy) + core (owned candidate) in the SAME database.
DBA review required. Additive only. Do not run against production without approval.
Does not copy data. Does not drop dbo objects.
*/
SET XACT_ABORT ON;

IF SCHEMA_ID(N'core') IS NULL
    EXEC(N'CREATE SCHEMA core AUTHORIZATION dbo;');
GO
