# Source Guidance

The plan is based on SQL Server and Azure SQL platform capabilities and should be validated against the exact hosting model:

- Azure SQL Database
- Azure SQL Managed Instance
- SQL Server on Azure VM
- AWS RDS for SQL Server
- another managed SQL Server platform

Use current official platform documentation for:

- Query Store;
- SQL Audit;
- Extended Events;
- dynamic management views;
- Microsoft.Build.Sql database projects;
- EF Core SQL Server migrations;
- Azure SQL read scale-out;
- elastic pools;
- elastic database/shard-map tooling;
- OpenTelemetry SQL client instrumentation.

Cloud-specific scripts must be reviewed before production execution.
