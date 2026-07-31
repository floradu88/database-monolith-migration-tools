# Solution and Project Structure

```text
DatabaseModernization/
|
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
│   │   └── scripts/                 # PowerShell setup/run (see HOW-TO-USE.md)

│   │
│   ├── MigrationControlPlane/
│   │   ├── MigrationControlPlane.Api/
│   │   ├── MigrationControlPlane.Worker/
│   │   ├── MigrationControlPlane.Domain/
│   │   ├── MigrationControlPlane.Infrastructure/
│   │   ├── MigrationControlPlane.Contracts/
│   │   └── MigrationControlPlane.Database/
│   │
│   ├── SourceMonolith/
│   │   ├── Monolith.Database.Foundation/
│   │   ├── Monolith.Database.Customer/
│   │   ├── Monolith.Database.Billing/
│   │   ├── Monolith.Database.Ordering/
│   │   ├── Monolith.Database.Reference/
│   │   ├── Monolith.Database.Reporting/
│   │   ├── Monolith.Database.Integration/
│   │   ├── Monolith.Database.Legacy/
│   │   └── Monolith.Database.Composite/
│   │
│   ├── DataServices/
│   │   ├── CustomerDataService/
│   │   │   ├── CustomerDataService.Api/
│   │   │   ├── CustomerDataService.Application/
│   │   │   ├── CustomerDataService.Domain/
│   │   │   ├── CustomerDataService.Infrastructure/
│   │   │   ├── CustomerDataService.Contracts/
│   │   │   ├── CustomerDataService.Database/
│   │   │   ├── CustomerDataService.Migrations/
│   │   │   └── CustomerDataService.Tests/
│   │   └── ...
│   │
│   └── BuildingBlocks/
│       ├── DataAccess.Abstractions/
│       ├── DataAccess.EfCore/
│       ├── DataAccess.Dapper/
│       ├── DataAccess.SqlServer/
│       ├── Observability/
│       ├── Security/
│       ├── Migration/
│       └── Testing/
│
├── sql/
│   ├── tracking/
│   ├── audit/
│   ├── query-store/
│   ├── extended-events/
│   ├── inventory/
│   └── deployment/
│
├── manifests/
│   ├── domains/
│   ├── objects/
│   ├── migration-waves/
│   └── shard-maps/
│
├── tests/
│   ├── Architecture.Tests/
│   ├── DatabaseContract.Tests/
│   ├── Migration.Tests/
│   ├── Performance.Tests/
│   └── Reconciliation.Tests/
│
└── docs/
```

## Separation of responsibilities

### SourceMonolith projects

Represent the current database exactly and allow ownership-based organization without physical extraction.

### Target Database projects

Represent the desired independent database state for a data service.

### EF migration projects

Optional and scoped to service-owned relational objects. They must not modify objects owned by the SQL project unless explicitly coordinated.

### Migration Control Plane

Owns orchestration and evidence, not business logic.

### DB Intelligence

Owns inventory, usage, dependencies, risk, and ownership mapping.
