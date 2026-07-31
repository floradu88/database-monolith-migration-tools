/*
Template for tracking database object and permission changes.
Review supported action groups for the exact SQL Server hosting model.
This records actions and actors, not table row values.

For SQL Server / Azure SQL Managed Instance, bind the database audit
specification to a pre-created and enabled SERVER AUDIT.

CREATE DATABASE AUDIT SPECIFICATION [DbSchemaSecurityAudit]
FOR SERVER AUDIT [YourServerAudit]
    ADD (SCHEMA_OBJECT_CHANGE_GROUP),
    ADD (DATABASE_OBJECT_PERMISSION_CHANGE_GROUP),
    ADD (DATABASE_PRINCIPAL_CHANGE_GROUP),
    ADD (DATABASE_ROLE_MEMBER_CHANGE_GROUP)
WITH (STATE = ON);
*/
