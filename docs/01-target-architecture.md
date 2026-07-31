# Target Architecture

## Transitional architecture

```text
Legacy Applications
       |
       | existing calls
       v
Compatibility/Data-Service Facades
       |
       +-----------------------+
       |                       |
       v                       v
Source Monolith DB       Target Domain DBs
       |                       |
       v                       v
DB Intelligence <------ Telemetry/Audit
       |
       v
Migration Control Plane
```

## Final architecture

```text
Consumers
   |
   +--> Customer Data Service --> Customer DB
   +--> Billing Data Service  --> Billing DB
   +--> Order Data Service    --> Order DB
   +--> Reference Data Service--> Reference DB
   |
   +--> Read Model / Search / Reporting
```

## Permanent components

- domain data services;
- domain databases;
- shared observability;
- DB Intelligence catalog;
- platform RBAC;
- data products and read models;
- shard routing where required.

## Temporary components

- migration control plane orchestration;
- compatibility stored procedures/views;
- source-to-target synchronization;
- shadow comparison;
- legacy access alerts.

## Data-service responsibilities

Each service owns:

- schema;
- migrations;
- runtime identity;
- API and event contracts;
- stored procedures and functions;
- performance budget;
- audit and tracing;
- backup/recovery requirements;
- data retention;
- scaling policy.

## Rules

- no shared write ownership;
- no consumer direct access to another domain database;
- no cross-database transaction by default;
- no new generic shared SQL credentials;
- no object deletion without static and runtime evidence;
- no service autoscaling without a database connection budget.
