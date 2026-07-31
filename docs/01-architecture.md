# 1. Architecture

## Objective

Decompose a shared SQL Server database monolith into application- or domain-owned data services while maintaining service continuity, auditability, rollback, and data correctness.

The program has three permanent or semi-permanent platforms:

1. **DB Intelligence Platform** — discovers and continuously maps applications, code references, SQL objects, runtime executions, permissions, and risks.
2. **Migration Control Plane** — plans and executes backfills, validations, cutovers, rollbacks, and legacy-object retirement.
3. **Domain Data Services** — permanent services that own schemas/databases and expose controlled APIs, commands, events, or approved read products.

## Current state

```text
App A ─┐
App B ─┼── Shared credentials ── SQL Server monolith
App C ─┤                         ├── dbo tables
Jobs ──┤                         ├── stored procedures/functions
BI ────┘                         ├── views/triggers
                                  └── cross-domain transactions
```

Typical risks:

- unknown consumers and unclear ownership;
- multiple applications writing the same tables;
- broad permissions and shared credentials;
- business logic hidden in procedures, functions, and triggers;
- reporting and batch workloads coupled to OLTP;
- cross-database or linked-server dependencies;
- inability to delete apparently unused objects safely.

## Transition architecture

```text
Repositories ──> Static Scanner ──────────────┐
                                               │
SQL metadata ──> SQL Object Extractor ─────────┼──> Intelligence Catalog
                                               │          │
Runtime SQL ──> Query Store / XE / Audit / DMV ┘          ├── dependency graph
                                                          ├── ownership workflow
                                                          └── migration backlog

Consumers ──> Data-service façade ──> existing monolith schema
                                      └── later redirected to target schema/database
```

Logical isolation precedes physical extraction:

```text
Ownership → domain schema → dedicated identity → service façade
→ eliminate shared writes → eliminate direct cross-domain reads
→ physical database extraction → legacy cleanup
```

## Target architecture

```text
Customer applications ──> Customer Data Service ──> Customer DB
Billing applications  ───> Billing Data Service  ──> Billing DB
Order applications    ───> Order Data Service    ──> Order DB

Cross-domain integration:
- synchronous REST/gRPC for commands and strongly consistent lookups;
- domain events and an outbox for propagation;
- CDC/read models for transitional integration and reporting;
- warehouse/lakehouse/replica for analytics.
```

## Core principles

### One accountable owner per object

Every table, view, procedure, function, trigger, and job has one approved owner. It may have many consumers, but only one owner.

### No shared write ownership

Only the owning data service can modify its authoritative data. Other applications use commands/APIs/events.

### API or event contracts over table contracts

Tables are implementation details. Consumers should depend on service contracts or controlled data products.

### Keep SQL where SQL is strong

Set-based transformations, bulk operations, and database-local consistency operations can remain in SQL, but must be owned, versioned, secured, tested, and observable by the data service.

### Evidence before deletion

Static analysis, SQL dependency metadata, Query Store, Extended Events, Audit, job/report inventory, and owner approval are combined. No single source proves non-use.
