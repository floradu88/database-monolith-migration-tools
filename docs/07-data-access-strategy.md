# Data Access Strategy

## Stable abstraction

```text
Application handlers
       |
       v
Domain/Query ports
       |
       +--> EF Core implementation
       +--> Dapper implementation
       +--> Stored procedure implementation
```

The API contract must not expose the chosen DAL.

## Default choices

- EF Core for ordinary CRUD and aggregate persistence.
- EF Core no-tracking projections for common reads.
- Dapper for tuned queries and explicit stored-procedure contracts.
- Stored procedures for large set-based operations and source compatibility.
- dedicated read models for cross-domain views.
- no synchronous fan-out for common UI queries.

## A/B and shadow evaluation

Use feature flags per operation:

```text
Operation: GetCustomerSummary
Authoritative: Dapper
Candidate: EF Core
Mode: Shadow
Traffic: 100% authoritative, 10% mirrored candidate
```

Compare:

- normalized result;
- p50/p95/p99;
- SQL CPU;
- logical reads;
- allocations;
- timeout/error rate;
- plan stability;
- connection usage.

## Write migration

For writes, use:

- contract tests;
- replay in non-production;
- canary tenants;
- idempotency;
- outbox;
- rollback routing.

Avoid duplicate production writes as a generic A/B technique.
