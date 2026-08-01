# 2. Project and Repository Structure

## Recommended solution

```text
DatabaseModernization.sln
├── src/
│   ├── DbIntelligence/
│   │   ├── DbIntelligence.Api/
│   │   ├── DbIntelligence.Cli/
│   │   ├── DbIntelligence.Worker/
│   │   ├── DbIntelligence.RepositoryScanner/
│   │   ├── DbIntelligence.SqlScanner/
│   │   ├── DbIntelligence.Domain/
│   │   ├── DbIntelligence.Infrastructure/
│   │   ├── DbIntelligence.Contracts/
│   │   ├── DbIntelligence.Web/
│   │   ├── DbIntelligence.Tests/
│   │   └── scripts/   # PowerShell; Initialize-DbIntelligenceNode.ps1 (fnm) + setup/run/index/batch
│   ├── DbMigrationControlPlane/
│   │   ├── DbMigration.Api/
│   │   ├── DbMigration.Worker/
│   │   ├── DbMigration.Domain/
│   │   ├── DbMigration.Infrastructure/
│   │   └── DbMigration.Contracts/
│   ├── DataServices/
│   │   ├── CustomerDataService/
│   │   ├── BillingDataService/
│   │   └── SharedReferenceDataService/
│   └── BuildingBlocks/
│       ├── DataService.Hosting/
│       ├── DataService.Security/
│       ├── DataService.Observability/
│       ├── DataService.Migrations/
│       ├── DataService.Messaging/
│       └── DataService.Testing/
├── database/
│   ├── intelligence/
│   ├── migration-control/
│   ├── source-monolith/
│   └── targets/
├── tools/
│   ├── repo-scanner/
│   ├── sql-exporter/
│   ├── dependency-graph/
│   ├── reconciliation/
│   └── deprecated-object-checker/
├── tests/
└── docs/
```

## DB Intelligence responsibilities

- scan .NET projects with Roslyn;
- detect ADO.NET, Dapper, EF Core raw SQL, mapped procedures/functions, `.sql` files, and connection configuration;
- extract SQL Server metadata and definitions;
- parse T-SQL using Microsoft.SqlServer.TransactSql.ScriptDom;
- collect Query Store, DMV, Extended Events, Audit, Agent job, and permissions data;
- normalize caller/application identity;
- build an evidence graph;
- expose search, reports, and ownership workflows;
- produce machine-readable migration manifests.

## Migration Control Plane responsibilities

- organize objects into migration waves;
- execute prechecks and target provisioning;
- backfill data;
- run reconciliation and shadow comparisons;
- orchestrate cutover and rollback;
- revoke legacy access;
- manage deprecation and deletion gates;
- preserve an immutable audit trail of approvals and outcomes.

It must not become a permanent business API.

## Domain data-service structure

```text
CustomerDataService/
├── CustomerDataService.Api/
├── CustomerDataService.Application/
├── CustomerDataService.Domain/
├── CustomerDataService.Infrastructure/
├── CustomerDataService.Contracts/
├── CustomerDataService.Database/
└── CustomerDataService.Tests/
```

### Database project ownership

The same team owns:

- API contracts;
- domain/application logic;
- schema and migrations;
- procedures/functions retained in SQL;
- runtime identities and grants;
- dashboards and alerts;
- backups, restore tests, RPO/RTO, and runbooks.

### Separate identities

```text
CustomerDataService.Runtime
CustomerDataService.Migrations
CustomerDataService.ReadOnly
CustomerDataService.Support
```

The runtime identity cannot modify schemas. The migration identity is never used by the running application.
