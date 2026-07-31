/*
SQL Server/Azure SQL Managed Instance template.
Create and enable a SERVER AUDIT first.
Tracks action execution, not before/after values.

CREATE DATABASE AUDIT SPECIFICATION [DbObjectAndDmlAudit]
FOR SERVER AUDIT [YourServerAudit]
    ADD (EXECUTE ON SCHEMA::[app] BY [ApplicationRuntimeRole]),
    ADD (INSERT  ON SCHEMA::[app] BY [ApplicationRuntimeRole]),
    ADD (UPDATE  ON SCHEMA::[app] BY [ApplicationRuntimeRole]),
    ADD (DELETE  ON SCHEMA::[app] BY [ApplicationRuntimeRole])
WITH (STATE = ON);
GO

Add SELECT only where required because volume can be high.
*/
