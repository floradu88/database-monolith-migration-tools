# Required Platform Decision Record

Before enabling production tracking, record the exact hosting model:

- Azure SQL Database;
- Azure SQL Managed Instance;
- SQL Server on Azure VM;
- AWS RDS for SQL Server;
- another managed SQL Server platform.

The decision must resolve:

- Extended Events scope and targets;
- SQL Audit destination and retention;
- SQL Agent availability;
- cross-database capabilities;
- read replicas and failover;
- backup/restore controls;
- identity and managed identity support;
- elastic pool or equivalent scaling;
- sharding/router implementation;
- permissions available to collectors.

Do not deploy platform-specific scripts until this ADR is approved.
